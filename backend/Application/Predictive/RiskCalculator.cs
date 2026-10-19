using Application.DTOs.Predictive;
using Domain.Entities;
using Domain.Enums;
namespace Application.Predictive;

public record RiskInput(DateTime At, IReadOnlyList<Incident> Incidents, IReadOnlyList<Alert> FailureAlerts, IReadOnlyList<Equipment> Equipments);

/// <summary>
/// Indice de risco por bairro (0-100), transparente e baseado em regras:
/// Score = 100 x (wDensidade x densidade + wFalhas x falhas + wPico x pico + wIndisp x indisponibilidade), cada fator normalizado em 0..1.
/// </summary>
public static class RiskCalculator
{
    private static readonly EquipmentStatus[] Unavailable = [EquipmentStatus.Malfunctioning, EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen];

    public static bool IsPeak(DateTime utc, PredictiveSettings s)
    {
        var hour = utc.AddHours(s.UtcOffsetHours).Hour;
        return s.PeakWindows.Any(w => hour >= w.Start && hour < w.End);
    }

    public static RiskLevel LevelOf(double score, PredictiveSettings s) =>
        score >= s.CriticalThreshold ? RiskLevel.Critical :
        score >= s.HighThreshold ? RiskLevel.High :
        score >= s.MediumThreshold ? RiskLevel.Medium : RiskLevel.Low;

    private record Row(string Name, List<Incident> Inc, List<Equipment> Eq, List<Alert> Rec);

    public static List<NeighborhoodRiskDto> Calculate(RiskInput input, PredictiveSettings s)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var at = input.At;
        var incidents = input.Incidents.Where(i => i.OccurredAt >= at.AddDays(-s.DensityWindowDays) && i.OccurredAt <= at).ToList();
        var alerts = input.FailureAlerts.Where(a => a.CreatedAt >= at.AddDays(-s.FailureWindowDays) && a.CreatedAt <= at).ToList();
        var equipments = input.Equipments.Where(e => e.Address != null && !string.IsNullOrWhiteSpace(e.Address.Neighborhood)).ToList();
        var equipById = equipments.ToDictionary(e => e.Id);

        var names = new Dictionary<string, string>(cmp);
        foreach (var n in incidents.Select(i => i.Neighborhood).Concat(equipments.Select(e => e.Address.Neighborhood)))
            if (!string.IsNullOrWhiteSpace(n)) names.TryAdd(n.Trim(), n.Trim());
        if (names.Count == 0) return [];

        var incByN = incidents.ToLookup(i => i.Neighborhood.Trim(), cmp);
        var eqByN = equipments.ToLookup(e => e.Address.Neighborhood.Trim(), cmp);
        var recurring = alerts.GroupBy(a => a.EquipmentId).Where(g => g.Count() >= s.RecurringFailureMinCount).Select(g => g.Key).ToHashSet();
        var recByN = alerts.Where(a => recurring.Contains(a.EquipmentId) && equipById.ContainsKey(a.EquipmentId))
            .ToLookup(a => equipById[a.EquipmentId].Address.Neighborhood.Trim(), cmp);

        var rows = names.Values.Select(n => new Row(n, incByN[n].ToList(), eqByN[n].ToList(), recByN[n].ToList())).ToList();
        double maxInc = rows.Max(r => r.Inc.Count);
        double maxRec = rows.Max(r => r.Rec.Count);
        var peakNow = IsPeak(at, s);

        static RiskFactorDto F(string name, string label, double raw, string rawDesc, double norm, double weight, string rule,
            string kind, IReadOnlyCollection<Guid> ids) =>
            new(name, label, Math.Round(raw, 2), rawDesc, Math.Round(norm, 4), weight, Math.Round(weight * norm * 100, 2), rule,
                kind, ids.Count, ids.Take(50).ToList());

        var result = new List<NeighborhoodRiskDto>();
        foreach (var r in rows)
        {
            var densityNorm = maxInc > 0 ? r.Inc.Count / maxInc : 0;
            var recNorm = maxRec > 0 ? r.Rec.Count / maxRec : 0;
            var peakIncidents = r.Inc.Where(i => IsPeak(i.OccurredAt, s)).ToList();
            var peakShare = r.Inc.Count > 0 ? (double)peakIncidents.Count / r.Inc.Count : 0;
            var peakNorm = peakNow ? 1.0 : peakShare;
            var unavailable = r.Eq.Where(e => Unavailable.Contains(e.Status)).ToList();
            var unavailNorm = r.Eq.Count > 0 ? (double)unavailable.Count / r.Eq.Count : 0;

            var factors = new List<RiskFactorDto>
            {
                F("IncidentDensity", "Densidade de ocorrencias", r.Inc.Count,
                    $"{r.Inc.Count} ocorrencia(s) nos ultimos {s.DensityWindowDays} dias",
                    densityNorm, s.Weights.Density,
                    $"contagem / maior contagem entre bairros ({maxInc:0}) = {densityNorm:0.##}", "Incident", r.Inc.Select(i => i.Id).ToList()),
                F("RecurringFailures", "Falhas recorrentes de equipamentos", r.Rec.Count,
                    $"{r.Rec.Count} falha(s) em equipamentos com {s.RecurringFailureMinCount}+ falhas nos ultimos {s.FailureWindowDays} dias",
                    recNorm, s.Weights.Failures,
                    $"falhas recorrentes / maior valor entre bairros ({maxRec:0}) = {recNorm:0.##}", "Alert", r.Rec.Select(a => a.Id).ToList()),
                F("PeakHour", "Horario de pico", Math.Round(peakNorm * 100, 1),
                    peakNow ? "Avaliacao dentro de janela de horario de pico" : $"{peakShare:P0} das ocorrencias recentes ocorreram em horario de pico",
                    peakNorm, s.Weights.Peak,
                    peakNow ? "fator = 1 (agora e horario de pico)" : "fator = proporcao de ocorrencias recentes em horario de pico",
                    "Incident", peakIncidents.Select(i => i.Id).ToList()),
                F("UnavailableEquipment", "Equipamentos indisponiveis", Math.Round(unavailNorm * 100, 1),
                    $"{unavailable.Count} de {r.Eq.Count} equipamento(s) indisponivel(is) (falha, manutencao ou furto)",
                    unavailNorm, s.Weights.Unavailability,
                    "fator = indisponiveis / total de equipamentos do bairro", "Equipment", unavailable.Select(e => e.Id).ToList()),
            };

            var score = Math.Round(factors.Sum(f => f.Points), 1);
            var level = LevelOf(score, s);
            var top = factors.OrderByDescending(f => f.Points).First();
            var pts = r.Inc.Select(i => (Lat: i.Latitude, Lon: i.Longitude))
                .Concat(r.Eq.Select(e => (Lat: e.Address.Latitude, Lon: e.Address.Longitude))).ToList();

            result.Add(new NeighborhoodRiskDto(r.Name, score, level,
                pts.Count > 0 ? pts.Average(p => p.Lat) : 0, pts.Count > 0 ? pts.Average(p => p.Lon) : 0, at,
                $"Risco {level} ({score:0.#}/100) em {r.Name}. Principal fator: {top.Label} ({top.Points:0.#} pts).", factors));
        }
        return result;
    }
}
