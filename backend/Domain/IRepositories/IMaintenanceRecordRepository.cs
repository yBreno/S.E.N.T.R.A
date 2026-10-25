using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;
public interface IMaintenanceRecordRepository : IRepository<MaintenanceRecord>
{
    Task<List<MaintenanceRecord>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
    Task<List<MaintenanceRecord>> GetByStatusAsync(MaintenanceStatus status, CancellationToken ct = default);
    Task<List<MaintenanceRecord>> GetRiskEvidenceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<bool> HasOverlapAsync(Guid equipmentId, DateTime start, DateTime? end, Guid? ignoreId = null, CancellationToken ct = default);
}
