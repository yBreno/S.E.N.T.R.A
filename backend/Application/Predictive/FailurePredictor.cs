using Application.DTOs.Predictive;
using Domain.Entities;
using Domain.Enums;
namespace Application.Predictive;


public static class FailurePredictor
{
    private static List<double> Gaps(List<Alert> l)
    {
        var r = new List<double>();
        for (var i = 1; i < l.Count; i++)
        {
            var days = (l[i].CreatedAt - l[i - 1].CreatedAt).TotalDays;
            if (days > 0) r.Add(days);
        }
        return r;
    }

    public static List<FailurePredictionDto> Predict(DateTime at, IReadOnlyList<Equipment> equipments, IReadOnlyList<Alert> failureAlerts,
        IReadOnlyList<MaintenanceRecord> closedMaintenances, PredictiveSettings s)
    {
        var alertsByEq = failureAlerts.Where(a => a.CreatedAt <= at).GroupBy(a => a.EquipmentId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).DistinctBy(a => a.CreatedAt).ToList());
        var typeGaps = equipments.GroupBy(e => e.Type).ToDictionary(g => g.Key,
            g => g.SelectMany(e => alertsByEq.TryGetValue(e.Id, out var l) ? Gaps(l) : new List<double>()).ToList());
        var lastMaint = closedMaintenances.Where(m => m.EndDate.HasValue && m.EndDate <= at).GroupBy(m => m.EquipmentId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.EndDate!.Value));

        var result = new List<FailurePredictionDto>();
        foreach (var e in equipments.Where(e => e.Status == EquipmentStatus.Operational))
        {
            alertsByEq.TryGetValue(e.Id, out var own);
            var ownGaps = own is null ? new List<double>() : Gaps(own);
            double mtbf; string basis; int n;
            if (ownGaps.Count >= 1) { mtbf = ownGaps.Average(); basis = "Equipment"; n = ownGaps.Count; }
            else if (typeGaps[e.Type].Count >= 1) { mtbf = typeGaps[e.Type].Average(); basis = "Type"; n = typeGaps[e.Type].Count; }
            else continue; // dados insuficientes para prever
            if (mtbf <= 0) continue;

            var restore = e.CreatedAt;
            if (own is { Count: > 0 } && own[^1].CreatedAt > restore) restore = own[^1].CreatedAt;
            if (lastMaint.TryGetValue(e.Id, out var m) && m > restore) restore = m;

            var since = Math.Max(0, (at - restore).TotalDays);
            var ratio = since / mtbf;
            var state = ratio >= 1 ? "Overdue" : ratio >= s.MtbfWarningRatio ? "Warning" : "Ok";
            var basisText = basis == "Equipment" ? "historico do proprio equipamento" : $"historico do tipo {e.Type}";
            result.Add(new FailurePredictionDto(e.Id, e.SerialNumber, e.Type.ToString(), e.Address?.Neighborhood ?? "",
                Math.Round(mtbf, 2), basis, n, Math.Round(since, 2), Math.Round(ratio, 3), Math.Round(Math.Max(0, mtbf - since), 2), state,
                $"MTBF {mtbf:0.#} dias ({basisText}, {n} intervalo(s)). Ultima restauracao em {restore:dd/MM/yyyy}: {since:0.#} dias = {ratio:P0} do MTBF " +
                $"(alerta preventivo a partir de {s.MtbfWarningRatio:P0}). Estimativa descritiva; razão não é probabilidade. Falha registrada é apenas proxy de restauração; dados escassos e ausência de falhas não foram calibrados.",
                own?.Select(a => a.Id).ToList() ?? []));
        }
        return result.OrderByDescending(p => p.Ratio).ToList();
    }
}
