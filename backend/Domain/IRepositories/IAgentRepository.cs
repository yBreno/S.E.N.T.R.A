using Domain.Entities;
namespace Domain.IRepositories;
public interface IAgentRepository : IRepository<Agent>
{
    Task<Agent?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CpfExistsAsync(string cpf, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> BadgeExistsAsync(string badge, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> UserLinkedAsync(Guid userId, Guid? ignoreAgentId = null, CancellationToken ct = default);
    Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default);
    Task<int> CountActiveAsync(CancellationToken ct = default);
}
