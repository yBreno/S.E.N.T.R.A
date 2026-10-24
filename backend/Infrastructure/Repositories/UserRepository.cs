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
}
