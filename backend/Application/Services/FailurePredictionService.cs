using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.Enums;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class FailurePredictionService(IEquipmentRepository equipments, IAlertRepository alerts, IMaintenanceRecordRepository maintenances,
    IOptions<PredictiveSettings> options, TimeProvider clock) : IFailurePredictionService
{
    public async Task<IReadOnlyList<FailurePredictionDto>> PredictAsync(bool onlyAtRisk, CancellationToken ct = default)
    {
        var s = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var eq = await equipments.GetAllAsync(ct);
        var failures = await alerts.GetFailureAlertsAsync(DateTime.MinValue, ct);
        var closed = await maintenances.GetByStatusAsync(MaintenanceStatus.Closed, ct);
        var all = FailurePredictor.Predict(now, eq, failures, closed, s);
        return onlyAtRisk ? all.Where(p => p.State != "Ok").ToList() : all;
    }
}
