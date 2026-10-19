using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
namespace Application.Services;

public class PredictiveAnalysisService(IFailurePredictionService predictions, IAnomalyService anomalies, IRiskService risk,
    IRecommendationService recommendations, IAlertRepository alerts, IUnitOfWork uow) : IPredictiveAnalysisService
{
    public async Task<PredictiveAnalysisResultDto> RunAsync(CancellationToken ct = default)
    {
        int alertsCreated = 0, recsCreated = 0;

        // 1) Previsao MTBF -> alerta preventivo + recomendacao de manutencao
        var atRisk = (await predictions.PredictAsync(true, ct)).ToList();
        foreach (var p in atRisk)
        {
            if (!await alerts.HasOpenPreventiveAsync(p.EquipmentId, ct))
            {
                await alerts.AddAsync(new Alert
                {
                    EquipmentId = p.EquipmentId,
                    Type = AlertType.Preventive,
                    Severity = p.State == "Overdue" ? AlertSeverity.High : AlertSeverity.Medium,
                    Message = $"Manutencao preventiva recomendada: {p.SerialNumber} atingiu {p.Ratio:P0} do MTBF ({p.MtbfDays:0.#} dias)."
                }, ct);
                alertsCreated++;
            }
            if (await recommendations.CreateForPredictionAsync(p, ct) is not null) recsCreated++;
        }

        // 2) Anomalias -> recomendacoes
        var found = (await anomalies.DetectAsync(ct)).ToList();
        foreach (var a in found)
            if (await recommendations.CreateForAnomalyAsync(a, ct) is not null) recsCreated++;

        // 3) Risco regional -> recomendacoes
        foreach (var r in (await risk.GetAllAsync(null, ct)).Where(r => r.Level >= RiskLevel.Medium))
            recsCreated += (await recommendations.GenerateForRiskAsync(r, ct)).Count;

        await uow.SaveChangesAsync(ct);
        return new PredictiveAnalysisResultDto(alertsCreated, recsCreated, found, atRisk);
    }
}
