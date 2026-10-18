using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;
public interface IMaintenanceRecordRepository : IRepository<MaintenanceRecord>
{
    Task<List<MaintenanceRecord>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
    Task<List<MaintenanceRecord>> GetByStatusAsync(MaintenanceStatus status, CancellationToken ct = default);
    /// <summary>Existe manutencao do equipamento sobreposta ao periodo [start, end]? (end nulo = aberto).</summary>
    Task<bool> HasOverlapAsync(Guid equipmentId, DateTime start, DateTime? end, Guid? ignoreId = null, CancellationToken ct = default);
}
