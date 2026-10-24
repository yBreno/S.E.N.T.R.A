using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class RecommendationRepository(AppDbContext context) : Repository<Recommendation>(context), IRecommendationRepository
{
    public Task<List<Recommendation>> GetByFilterAsync(RecommendationStatus? status, RecommendationType? type, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking();
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (type.HasValue) q = q.Where(r => r.Type == type.Value);
        return q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public Task<bool> ExistsPendingAsync(RecommendationType type, string neighborhood, Guid? equipmentId, CancellationToken ct = default) =>
        Set.AnyAsync(r => r.Status == RecommendationStatus.Pending && r.Type == type && r.EquipmentId == equipmentId &&
            (equipmentId != null || r.Neighborhood == neighborhood), ct);

    public Task<int> CountPendingAsync(CancellationToken ct = default) => Set.CountAsync(r => r.Status == RecommendationStatus.Pending, ct);
}
