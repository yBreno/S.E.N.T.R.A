using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class IncidentRepository(AppDbContext context) : Repository<Incident>(context), IIncidentRepository
{
    public Task<List<Incident>> SearchAsync(IncidentFilter f, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking();
        if (f.From.HasValue) q = q.Where(i => i.OccurredAt >= f.From.Value);
        if (f.To.HasValue) q = q.Where(i => i.OccurredAt <= f.To.Value);
        if (f.Type.HasValue) q = q.Where(i => i.Type == f.Type.Value);
        if (!string.IsNullOrWhiteSpace(f.Neighborhood))
        {
            var n = f.Neighborhood.Trim();
            q = q.Where(i => i.Neighborhood == n);
        }
        return q.OrderBy(i => i.OccurredAt).ToListAsync(ct);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default) => Set.AnyAsync(ct);
}
