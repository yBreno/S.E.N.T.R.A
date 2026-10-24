using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<bool> CpfExistsAsync(string cpf, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> HasRoleAsync(Guid userId, UserRole role, CancellationToken ct = default);
}
