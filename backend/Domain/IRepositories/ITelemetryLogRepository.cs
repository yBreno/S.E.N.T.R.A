using Domain.Entities;
namespace Domain.IRepositories;
public interface ITelemetryLogRepository : IRepository<TelemetryLog>
{
    Task<List<TelemetryLog>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
}
