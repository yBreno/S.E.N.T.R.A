using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class AlertRepository(AppDbContext context) : Repository<Alert>(context), IAlertRepository
{
    public Task<List<Alert>> GetByStatusAsync(AlertStatus? status, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking();
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);
        return q.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }
    public Task<List<Alert>> GetOpenByEquipmentAsync(Guid equipmentId, CancellationToken ct = default) =>
        Set.Where(a => a.EquipmentId == equipmentId && a.Status == AlertStatus.Open).ToListAsync(ct);
}
