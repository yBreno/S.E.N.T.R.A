using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;

public record EquipmentFilter(EquipmentType? Type, EquipmentStatus? Status, string? Neighborhood, Guid? OrganizationId);

public interface IEquipmentRepository : IRepository<Equipment>
{
    /// <summary>Retorna o equipamento com Address e Organization carregados.</summary>
    Task<Equipment?> GetDetailedAsync(Guid id, CancellationToken ct = default);
    Task<List<Equipment>> SearchAsync(EquipmentFilter filter, CancellationToken ct = default);
    Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> LicensePlateExistsAsync(string plate, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> AgentHasVehicleAsync(Guid agentId, Guid? ignoreVehicleId = null, CancellationToken ct = default);
    Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<Dictionary<EquipmentStatus, int>> CountByStatusAsync(CancellationToken ct = default);
}
