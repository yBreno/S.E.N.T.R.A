using Application.DTOs.Dashboard;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Enums;
using Domain.IRepositories;
namespace Application.Services;

public class DashboardService(IEquipmentRepository equipments, IAgentRepository agents, IRiskService risk, IRecommendationRepository recommendations,
    IAnomalyService anomalies, IFailurePredictionService predictions) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var total = await equipments.CountAsync(ct);
        var counts = await equipments.CountByStatusAsync(ct);
        var byStatus = Enum.GetValues<EquipmentStatus>().ToDictionary(s => s.ToString(), s => counts.GetValueOrDefault(s));
        var operational = counts.GetValueOrDefault(EquipmentStatus.Operational);
        var rate = total == 0 ? 0 : Math.Round(operational * 100.0 / total, 2);

        var top = (await risk.GetAllAsync(null, ct)).Take(5).Select(r => new RiskSummaryDto(r.Neighborhood, r.Score, r.Level)).ToList();
        var predictive = new PredictiveSummaryDto(top, await recommendations.CountPendingAsync(ct),
            (await anomalies.DetectAsync(ct)).Count, (await predictions.PredictAsync(true, ct)).Count);

        return new DashboardDto(total, byStatus, rate, await agents.CountActiveAsync(ct), predictive);
    }
}
