using Application.DTOs.Predictive;
using Domain.Entities;
namespace Application.Predictive;


public static class AnomalyDetector
{
    public static List<AnomalyDto> Detect(DateTime at, IReadOnlyList<Incident> incidents, IReadOnlyList<Alert> failureAlerts,
        IReadOnlyList<Equipment> equipments, PredictiveSettings s)
    {
        var result = new List<AnomalyDto>();
        var win = TimeSpan.FromHours(s.AnomalyWindowHours);

        foreach (var g in incidents.GroupBy(i => i.Neighborhood.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var list = g.ToList();
            var current = list.Where(i => i.OccurredAt > at - win && i.OccurredAt <= at).ToList();
            if (current.Count < s.AnomalyMinCount) continue;

            var samples = Enumerable.Range(1, Math.Max(1, s.AnomalyBaselineWeeks)).Select(k =>
            {
                var end = at.AddDays(-7 * k);
                var start = end - win;
                return (double)list.Count(i => i.OccurredAt > start && i.OccurredAt <= end);
            }).ToList();

            var mean = samples.Average();
            var std = samples.Count > 1 ? Math.Sqrt(samples.Sum(x => Math.Pow(x - mean, 2)) / (samples.Count - 1)) : 0;
            var threshold = mean + s.AnomalySigma * std;
            if (current.Count <= threshold) continue;

            double? z = std > 0 ? (current.Count - mean) / std : null;
            result.Add(new AnomalyDto("VolumeSpike", g.Key, g.Key, null, current.Count, Math.Round(mean, 2), Math.Round(std, 2),
                Math.Round(threshold, 2), z.HasValue ? Math.Round(z.Value, 2) : null,
                $"{current.Count} ocorrencia(s) nas ultimas {s.AnomalyWindowHours}h em {g.Key}. Historico das mesmas {s.AnomalyWindowHours}h nos ultimos " +
                $"{samples.Count} mesmos dias da semana: media {mean:0.##}, desvio-padrao {std:0.##}. Limite = media + {s.AnomalySigma}x desvio = {threshold:0.##}.",
                current.Count, current.Select(i => i.Id).Take(50).ToList(), at));
        }

        var eqById = equipments.ToDictionary(e => e.Id);
        var from = at.AddDays(-s.FailureWindowDays);
        foreach (var g in failureAlerts.Where(a => a.CreatedAt > from && a.CreatedAt <= at).GroupBy(a => a.EquipmentId)
                     .Where(g => g.Count() >= s.RepeatedFailureCount))
        {
            eqById.TryGetValue(g.Key, out var eq);
            var hood = eq?.Address?.Neighborhood ?? "";
            var serial = eq?.SerialNumber ?? g.Key.ToString();
            result.Add(new AnomalyDto("RepeatedFailure", serial, hood, g.Key, g.Count(), 0, 0, s.RepeatedFailureCount - 1, null,
                $"Equipamento {serial} falhou {g.Count()} vez(es) nos ultimos {s.FailureWindowDays} dias (limite de normalidade: {s.RepeatedFailureCount - 1}).",
                g.Count(), g.Select(a => a.Id).Take(50).ToList(), at));
        }
        return result;
    }
}
