using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class UserAuditRepository(AppDbContext db) : IUserAuditRepository
{
    public async Task AddAsync(UserAuditEvent entry, CancellationToken ct = default) => await db.UserAuditEvents.AddAsync(entry, ct);
    public async Task<(IReadOnlyList<UserAuditEvent> Items, int Total)> SearchAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.UserAuditEvents.AsNoTracking().Where(e => e.UserId == userId);
        var total = await query.CountAsync(ct);
        return (await query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total);
    }
}
