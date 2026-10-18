using Domain.Entities;
namespace Domain.IRepositories;
public interface IOrganizationRepository : IRepository<Organization>
{
    Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken ct = default);
    Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default);
}
