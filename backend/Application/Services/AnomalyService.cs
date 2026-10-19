using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class AnomalyService(IIncidentRepository incidents, IAlertRepository alerts, IEquipmentRepository equipments,
    IOptions<PredictiveSettings> options, TimeProvider clock) : IAnomalyService
{
    public async Task<IReadOnlyList<AnomalyDto>> DetectAsync(CancellationToken ct = default)
    {
        var s = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var inc = await incidents.SearchAsync(new IncidentFilter(now.AddDays(-(7 * s.AnomalyBaselineWeeks + 1)), now, null, null), ct);
        var failures = await alerts.GetFailureAlertsAsync(now.AddDays(-s.FailureWindowDays), ct);
        var eq = await equipments.GetAllAsync(ct);
        return AnomalyDetector.Detect(now, inc, failures, eq, s);
    }
}
