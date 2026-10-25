using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        Set.AnyAsync(x => x.Email == email.Trim().ToLowerInvariant(), ct);
    public Task<bool> CpfExistsAsync(string cpf, CancellationToken ct = default) => Set.AnyAsync(x => x.Cpf == cpf, ct);
    public Task<bool> AnyAsync(CancellationToken ct = default) => Set.AnyAsync(ct);
    public Task<bool> HasRoleAsync(Guid userId, UserRole role, CancellationToken ct = default) =>
        Set.AnyAsync(user => user.Id == userId && user.Role == role, ct);
    public Task<bool> HasSessionAsync(Guid userId, UserRole role, long authVersion, CancellationToken ct = default) =>
        Set.AnyAsync(user => user.Id == userId && user.Role == role && user.IsActive && user.AuthVersion == authVersion, ct);
    public Task<bool> EmailInUseAsync(string email, Guid? exceptId, CancellationToken ct = default) =>
        Set.AnyAsync(user => user.Email == email.Trim().ToLowerInvariant() && user.Id != exceptId, ct);
    public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
        Set.CountAsync(user => user.IsActive && user.Role == UserRole.Admin, ct);
    public async Task<(IReadOnlyList<User> Items, int Total)> SearchAsync(string? search, UserRole? role, bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant(); query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.Contains(term));
        }
        if (role.HasValue) query = query.Where(u => u.Role == role);
        if (active.HasValue) query = query.Where(u => u.IsActive == active);
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(u => u.Name).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total);
    }
}
