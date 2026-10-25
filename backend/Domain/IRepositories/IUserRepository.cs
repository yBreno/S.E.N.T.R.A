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
    Task<bool> HasSessionAsync(Guid userId, UserRole role, long authVersion, CancellationToken ct = default);
    Task<bool> EmailInUseAsync(string email, Guid? exceptId, CancellationToken ct = default);
    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int Total)> SearchAsync(string? search, UserRole? role, bool? active, int page, int pageSize, CancellationToken ct = default);
}
