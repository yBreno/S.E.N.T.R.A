using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Mappings;
using Application.Predictive;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class RecommendationService(IRecommendationRepository repo, IAgentRepository agents, IEquipmentRepository equipments,
    IUnitOfWork uow, IOptions<PredictiveSettings> options, TimeProvider clock, IMaintenanceService maintenance,
    IMaintenanceRecordRepository maintenanceRecords) : IRecommendationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<Recommendation> LoadAsync(Guid id, CancellationToken ct) =>
        await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Recomendacao {id} nao encontrada.");

    public async Task<IReadOnlyList<RecommendationResponseDto>> GetAllAsync(RecommendationStatus? status, bool maintenanceOnly, CancellationToken ct = default) =>
        (await repo.GetByFilterAsync(status, maintenanceOnly ? RecommendationType.SendMaintenance : null, ct)).Select(r => r.ToResponse()).ToList();

    public async Task<RecommendationResponseDto> GetByIdAsync(Guid id, bool maintenanceOnly, CancellationToken ct = default)
    {
        var r = await LoadAsync(id, ct);
        if (maintenanceOnly && r.Type != RecommendationType.SendMaintenance) throw new NotFoundException($"Recomendacao {id} nao encontrada.");
        return r.ToResponse();
    }

    public async Task<RecommendationResponseDto> AcceptAsync(Guid id, CancellationToken ct = default)
    {
        var r = await LoadAsync(id, ct);
        if (r.Status == RecommendationStatus.Accepted) return r.ToResponse();
        if (r.Status != RecommendationStatus.Pending) throw new BusinessRuleException("A recomendacao ja foi descartada.");
        if (r.Type == RecommendationType.SendMaintenance)
        {
            if (r.EquipmentId is not { } eid)
                throw new BusinessRuleException("Selecione um equipamento e agende sua manutencao; esta recomendacao se refere a um bairro.");
            await maintenance.PrepareRecommendationAsync(eid, r.Id,
                clock.GetUtcNow().UtcDateTime.AddDays(options.Value.PreventiveScheduleDays), r.Reason, ct);
        }
        r.Accept();
        await uow.SaveChangesAsync(ct);
        return r.ToResponse();
    }

    public async Task<RecommendationResponseDto> DismissAsync(Guid id, CancellationToken ct = default)
    {
        var r = await LoadAsync(id, ct);
        r.Dismiss();
        await uow.SaveChangesAsync(ct);
        return r.ToResponse();
    }

    private async Task<List<object>> SuggestAgentsAsync(double lat, double lon, CancellationToken ct) =>
        (await agents.GetAllAsync(ct)).Where(a => a.Availability == AgentAvailability.Available)
            .Select(a => new { Dist = GeoMath.DistanceKm(lat, lon, a.Latitude, a.Longitude), Agent = a })
            .OrderBy(x => x.Dist).Take(3)
            .Select(x => (object)new { agentId = x.Agent.Id, name = x.Agent.Name, currentNeighborhood = x.Agent.Neighborhood, distanceKm = Math.Round(x.Dist, 2) })
            .ToList();

    private Recommendation Build(RecommendationType type, string neighborhood, string reason, double? score, Guid? equipmentId, object explanation) => new()
    {
        Type = type,
        Neighborhood = neighborhood,
        Reason = reason.Length > 500 ? reason[..500] : reason,
        RiskScore = score,
        EquipmentId = equipmentId,
        ExplanationJson = JsonSerializer.Serialize(explanation, Json)
    };

    public async Task<IReadOnlyList<RecommendationResponseDto>> GenerateForRiskAsync(NeighborhoodRiskDto risk, CancellationToken ct = default)
    {
        var s = options.Value;
        var created = new List<Recommendation>();

        if (risk.Level >= RiskLevel.High && !await repo.ExistsPendingAsync(RecommendationType.DeployAgent, risk.Neighborhood, null, ct))
        {
            created.Add(Build(RecommendationType.DeployAgent, risk.Neighborhood,
                $"Risco {risk.Level} ({risk.Score:0.#}/100) em {risk.Neighborhood}: deslocar agentes preventivamente.", risk.Score, null,
                new
                {
                    trigger = "RiskLevel",
                    rule = $"Nivel de risco >= High (score >= {s.HighThreshold})",
                    risk,
                    suggestedAgents = await SuggestAgentsAsync(risk.Latitude, risk.Longitude, ct)
                }));
        }

        var equipmentFactors = risk.Factors.Where(f => (f.Name == "RecurringFailures" || f.Name == "UnavailableEquipment") && f.RawValue > 0).ToList();
        if (risk.Level >= RiskLevel.Medium && equipmentFactors.Count > 0 &&
            !await repo.ExistsPendingAsync(RecommendationType.SendMaintenance, risk.Neighborhood, null, ct))
        {
            created.Add(Build(RecommendationType.SendMaintenance, risk.Neighborhood,
                $"Risco {risk.Level} em {risk.Neighborhood} com equipamentos em falha/indisponiveis: enviar equipe de manutencao.", risk.Score, null,
                new
                {
                    trigger = "RiskLevelWithEquipmentFactors",
                    rule = $"Nivel de risco >= Medium (score >= {s.MediumThreshold}) e fator de falhas recorrentes ou indisponibilidade > 0",
                    risk,
                    equipmentFactors
                }));
        }

        return await SaveAsync(created, ct);
    }

    public async Task<RecommendationResponseDto?> CreateForAnomalyAsync(AnomalyDto a, CancellationToken ct = default)
    {
        var type = a.Kind == "RepeatedFailure" ? RecommendationType.SendMaintenance : RecommendationType.DeployAgent;
        if (type == RecommendationType.SendMaintenance && a.EquipmentId is { } id && await HasMaintenanceAsync(id, ct)) return null;
        if (await repo.ExistsPendingAsync(type, a.Neighborhood, a.EquipmentId, ct)) return null;

        object extra = new { };
        if (type == RecommendationType.DeployAgent)
        {
            var risk = await ResolveCentroidAsync(a, ct);
            extra = new { suggestedAgents = risk is null ? new List<object>() : await SuggestAgentsAsync(risk.Value.lat, risk.Value.lon, ct) };
        }

        var reason = type == RecommendationType.DeployAgent
            ? $"Anomalia de volume em {a.Neighborhood}: {a.Current:0} ocorrencias (limite {a.Threshold:0.#}). Deslocar agentes."
            : $"Falhas repetidas no equipamento {a.Subject}: enviar manutencao.";
        var rec = Build(type, a.Neighborhood, reason, null, a.EquipmentId,
            new { trigger = "Anomaly", rule = a.Kind == "VolumeSpike" ? "atual > media + k x desvio-padrao" : "falhas >= limite na janela", anomaly = a, extra });
        return (await SaveAsync([rec], ct)).FirstOrDefault();
    }

    private async Task<(double lat, double lon)?> ResolveCentroidAsync(AnomalyDto a, CancellationToken ct)
    {
        var list = (await agents.GetAllAsync(ct)).Where(x => string.Equals(x.Neighborhood, a.Neighborhood, StringComparison.OrdinalIgnoreCase)).ToList();
        if (list.Count > 0) return (list.Average(x => x.Latitude), list.Average(x => x.Longitude));
        var eq = (await equipments.GetAllAsync(ct)).Where(e => e.Address != null &&
            string.Equals(e.Address.Neighborhood, a.Neighborhood, StringComparison.OrdinalIgnoreCase)).ToList();
        return eq.Count > 0 ? (eq.Average(e => e.Address.Latitude), eq.Average(e => e.Address.Longitude)) : null;
    }

    public async Task<RecommendationResponseDto?> CreateForPredictionAsync(FailurePredictionDto p, CancellationToken ct = default)
    {
        if (await HasMaintenanceAsync(p.EquipmentId, ct)) return null;
        if (await repo.ExistsPendingAsync(RecommendationType.SendMaintenance, p.Neighborhood, p.EquipmentId, ct)) return null;
        var rec = Build(RecommendationType.SendMaintenance, p.Neighborhood,
            $"Manutencao preventiva em {p.SerialNumber}: {p.Ratio:P0} do MTBF ({p.MtbfDays:0.#} dias).", null, p.EquipmentId,
            new { trigger = "MtbfPrediction", rule = $"dias desde a ultima restauracao / MTBF >= {options.Value.MtbfWarningRatio:P0}", prediction = p });
        return (await SaveAsync([rec], ct)).FirstOrDefault();
    }

    private async Task<bool> HasMaintenanceAsync(Guid equipmentId, CancellationToken ct) =>
        (await maintenanceRecords.GetByEquipmentAsync(equipmentId, ct)).Any(m => m.Status != MaintenanceStatus.Closed);

    private async Task<IReadOnlyList<RecommendationResponseDto>> SaveAsync(List<Recommendation> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        foreach (var r in list) await repo.AddAsync(r, ct);
        await uow.SaveChangesAsync(ct);
        return list.Select(r => r.ToResponse()).ToList();
    }
}
