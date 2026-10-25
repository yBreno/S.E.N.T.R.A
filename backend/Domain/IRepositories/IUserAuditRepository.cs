using Domain.Entities;
namespace Domain.IRepositories;

public interface IUserAuditRepository
{
    Task AddAsync(UserAuditEvent entry, CancellationToken ct = default);
    Task<(IReadOnlyList<UserAuditEvent> Items, int Total)> SearchAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}
