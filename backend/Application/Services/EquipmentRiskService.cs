using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class EquipmentRiskService(IEquipmentRepository equipments, IAlertRepository alerts,
    IMaintenanceRecordRepository maintenance, ITelemetryLogRepository telemetry,
    IOptions<PredictiveSettings> options, TimeProvider clock) : IEquipmentRiskService
{
    public async Task<IReadOnlyList<EquipmentRiskDto>> GetAllAsync(CancellationToken ct = default)
    {
        var at = clock.GetUtcNow().UtcDateTime;
        var days = Math.Max(1, options.Value.FailureWindowDays);
        // Um lote por fonte, sem N+1 e sem paralelizar consultas no mesmo DbContext.
        var inventory = await equipments.GetAllAsync(ct);
        var failures = await alerts.GetFailureAlertsAsync(at.AddDays(-days), ct);
        var orders = await maintenance.GetRiskEvidenceAsync(at.AddDays(-days), at, ct);
        var signals = await telemetry.GetSignalsSinceAsync(at.AddDays(-days), ct);
        return EquipmentRiskCalculator.Calculate(at, inventory, failures, orders, signals, days);
    }
}
