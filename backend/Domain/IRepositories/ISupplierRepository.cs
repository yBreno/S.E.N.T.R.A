using Domain.Entities;
namespace Domain.IRepositories;
public interface ISupplierRepository : IRepository<Supplier>
{
    Task<bool> CnpjExistsAsync(string cnpj, Guid? ignoreId = null, CancellationToken ct = default);
}
