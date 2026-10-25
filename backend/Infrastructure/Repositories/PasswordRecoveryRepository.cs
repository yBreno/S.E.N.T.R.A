using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public sealed class PasswordRecoveryRepository(AppDbContext db) : IPasswordRecoveryRepository
{
    public Task<PasswordResetToken?> FindTokenAsync(string hash, CancellationToken ct) => db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
    public Task<PasswordResetToken?> ForUserAsync(Guid userId, CancellationToken ct) => db.PasswordResetTokens.SingleOrDefaultAsync(t => t.UserId == userId, ct);
    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
    public async Task<bool> TakeLimitAsync(string key, int maximum, DateTime now, CancellationToken ct)
    {
        var row = await db.PasswordRecoveryLimits.FindAsync([key], ct);
        // Pruning may have scheduled this same expired key for removal in this transaction.
        // Reuse must persist its new window instead of deleting the first counted attempt.
        if (row is not null && db.Entry(row).State == EntityState.Deleted) db.Entry(row).State = EntityState.Modified;
        if (row is null) { row = new() { Key = key, ExpiresAt = now.AddMinutes(15) }; db.PasswordRecoveryLimits.Add(row); }
        if (row.ExpiresAt <= now) { row.Count = 0; row.ExpiresAt = now.AddMinutes(15); }
        if (row.Count >= maximum) return false;
        row.Count++; return true;
    }
    public async Task PruneLimitsAsync(DateTime now, CancellationToken ct) =>
        db.PasswordRecoveryLimits.RemoveRange(await db.PasswordRecoveryLimits.Where(x => x.ExpiresAt < now.AddDays(-1)).OrderBy(x => x.ExpiresAt).Take(200).ToListAsync(ct));
}
