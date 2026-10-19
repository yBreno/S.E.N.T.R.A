using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class HeatmapService(IIncidentRepository incidents, IRiskService risk, IOptions<PredictiveSettings> options, TimeProvider clock) : IHeatmapService
{
    public async Task<HeatmapDto> GetAsync(HeatmapQueryDto q, CancellationToken ct = default)
    {
        if (q.FromHour is < 0 or > 23 || q.ToHour is < 0 or > 23)
            throw new BusinessRuleException("FromHour/ToHour devem estar entre 0 e 23.");
        if ((q.FromHour.HasValue) != (q.ToHour.HasValue))
            throw new BusinessRuleException("Informe FromHour e ToHour juntos.");

        var s = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var to = q.To ?? now;
        var from = q.From ?? to.AddDays(-s.DensityWindowDays);
        if (from > to) throw new BusinessRuleException("From deve ser anterior a To.");

        List<HeatPointDto> points;
        if (q.Metric == HeatmapMetric.Risk)
        {
            var risks = await risk.GetAllAsync(q.To, ct);
            points = risks
                .Where(r => string.IsNullOrWhiteSpace(q.Neighborhood) || string.Equals(r.Neighborhood, q.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(r => new HeatPointDto(r.Latitude, r.Longitude, Math.Round(r.Score / 100, 3), r.Neighborhood, $"{r.Level} ({r.Score:0.#})"))
                .ToList();
        }
        else
        {
            var type = q.Metric == HeatmapMetric.Failures ? IncidentType.EquipmentFailure : q.Type;
            var list = await incidents.SearchAsync(new IncidentFilter(from, to, q.Neighborhood, type), ct);
            points = list.Where(i =>
                {
                    var local = i.OccurredAt.AddHours(s.UtcOffsetHours);
                    if (q.DayOfWeek.HasValue && local.DayOfWeek != q.DayOfWeek.Value) return false;
                    if (q.FromHour is { } fh && q.ToHour is { } th)
                    {
                        var h = local.Hour;
                        return fh <= th ? h >= fh && h <= th : h >= fh || h <= th;
                    }
                    return true;
                })
                .Select(i => new HeatPointDto(i.Latitude, i.Longitude, ((int)i.Severity + 1) / 4.0, i.Neighborhood, i.Type.ToString()))
                .ToList();
        }

        return new HeatmapDto(q.Metric, from, to, points.Count, points.Count > 0 ? points.Max(p => p.Intensity) : 0, points);
    }
}
