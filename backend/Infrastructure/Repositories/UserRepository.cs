using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower(), ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        Set.AnyAsync(x => x.Email == email.Trim().ToLower(), ct);
    public Task<bool> CpfExistsAsync(string cpf, CancellationToken ct = default) => Set.AnyAsync(x => x.Cpf == cpf, ct);
    public Task<bool> AnyAsync(CancellationToken ct = default) => Set.AnyAsync(ct);
}
