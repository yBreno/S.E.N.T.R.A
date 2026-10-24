using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.Enums;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class PredictiveOperationsService(IEquipmentRiskService equipment, IRiskService risk, IRecommendationService recommendations,
    IOptions<PredictiveAutomationSettings> options, PredictiveExecutionState state, IPredictiveWriteCoordinator coordinator,
    TimeProvider clock) : IPredictiveOperationsService
{
    public PredictiveAutomationStatusDto GetStatus() => new(options.Value.Enabled, options.Value.IntervalMinutes, coordinator.SupportsMultipleInstances, state.Snapshot());
    public async Task<PredictiveOperationsDto> GetAsync(CancellationToken ct = default)
    {
        var equipments = await equipment.GetAllAsync(ct);
        var neighborhoods = await risk.GetAllAsync(null, ct);
        var pending = await recommendations.GetAllAsync(RecommendationStatus.Pending, false, ct);
        return new(clock.GetUtcNow().UtcDateTime, equipments.Where(e => e.Level == RiskLevel.High).ToList(), neighborhoods, pending, GetStatus());
    }
}
