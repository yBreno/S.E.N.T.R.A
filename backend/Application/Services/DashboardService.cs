using Application.DTOs.Dashboard;
using Application.Interfaces.IServices;
using Domain.Enums;
using Domain.IRepositories;
namespace Application.Services;

public class DashboardService(IEquipmentRepository equipments, IAgentRepository agents) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var total = await equipments.CountAsync(ct);
        var counts = await equipments.CountByStatusAsync(ct);
        var byStatus = Enum.GetValues<EquipmentStatus>().ToDictionary(s => s.ToString(), s => counts.GetValueOrDefault(s));
        var operational = counts.GetValueOrDefault(EquipmentStatus.Operational);
        var rate = total == 0 ? 0 : Math.Round(operational * 100.0 / total, 2);
        return new DashboardDto(total, byStatus, rate, await agents.CountActiveAsync(ct));
    }
}
