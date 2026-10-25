using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;

public record EquipmentFilter(EquipmentType? Type, EquipmentStatus? Status, string? Neighborhood);

public interface IEquipmentRepository : IRepository<Equipment>
{
    Task<Equipment?> GetDetailedAsync(Guid id, CancellationToken ct = default);
    Task<List<Equipment>> SearchAsync(EquipmentFilter filter, CancellationToken ct = default);
    Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> LicensePlateExistsAsync(string plate, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> AgentHasVehicleAsync(Guid agentId, Guid? ignoreVehicleId = null, CancellationToken ct = default);
    Task<List<Vehicle>> GetVehiclesByAgentAsync(Guid agentId, CancellationToken ct = default);
    Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<Dictionary<EquipmentStatus, int>> CountByStatusAsync(CancellationToken ct = default);
}
