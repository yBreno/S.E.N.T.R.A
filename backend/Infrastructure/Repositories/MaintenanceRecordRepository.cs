using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class MaintenanceRecordRepository(AppDbContext context) : Repository<MaintenanceRecord>(context), IMaintenanceRecordRepository
{
    private IQueryable<MaintenanceRecord> Detailed => Set.Include(m => m.Equipment);

    public override Task<MaintenanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Detailed.FirstOrDefaultAsync(m => m.Id == id, ct);
    public override Task<List<MaintenanceRecord>> GetAllAsync(CancellationToken ct = default) =>
        Set.AsNoTracking().OrderByDescending(m => m.InitDate).ToListAsync(ct);
    public Task<List<MaintenanceRecord>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(m => m.EquipmentId == equipmentId).OrderByDescending(m => m.InitDate).ToListAsync(ct);
    public Task<List<MaintenanceRecord>> GetByStatusAsync(MaintenanceStatus status, CancellationToken ct = default) =>
        Set.AsNoTracking().Where(m => m.Status == status).OrderByDescending(m => m.InitDate).ToListAsync(ct);

    public Task<bool> HasOverlapAsync(Guid equipmentId, DateTime start, DateTime? end, Guid? ignoreId = null, CancellationToken ct = default)
    {
        var newEnd = end ?? DateTime.MaxValue;
        return Set.AnyAsync(m => m.EquipmentId == equipmentId && m.Id != ignoreId
            && m.InitDate <= newEnd && (m.EndDate ?? DateTime.MaxValue) >= start, ct);
    }
}
