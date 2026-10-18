using Domain.Entities;
namespace Domain.IRepositories;
public interface IAddressRepository : IRepository<Address>
{
    Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default);
}
