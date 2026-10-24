using Domain.Entities;
namespace Domain.IRepositories;
public interface ITelemetryLogRepository : IRepository<TelemetryLog>
{
    Task<List<TelemetryLog>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
    Task<List<TelemetryLog>> GetSignalsSinceAsync(DateTime since, CancellationToken ct = default);
}
