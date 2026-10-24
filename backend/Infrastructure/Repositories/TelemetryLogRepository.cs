using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public class TelemetryLogRepository(AppDbContext context) : Repository<TelemetryLog>(context), ITelemetryLogRepository
{
    public Task<List<TelemetryLog>> GetSignalsSinceAsync(DateTime since, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(t => t.Timestamp >= since)
            .Select(t => new TelemetryLog { Id = t.Id, EquipmentId = t.EquipmentId, Timestamp = t.Timestamp, EventType = t.EventType })
            .ToListAsync(ct);

    public Task<List<TelemetryLog>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(t => t.EquipmentId == equipmentId).OrderByDescending(t => t.Timestamp).ToListAsync(ct);
}
