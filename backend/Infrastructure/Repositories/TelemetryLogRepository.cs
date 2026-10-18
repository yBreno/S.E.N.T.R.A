using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public class TelemetryLogRepository(AppDbContext context) : Repository<TelemetryLog>(context), ITelemetryLogRepository
{
    public Task<List<TelemetryLog>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(t => t.EquipmentId == equipmentId).OrderByDescending(t => t.Timestamp).ToListAsync(ct);
}
