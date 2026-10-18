using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class SupplierRepository(AppDbContext context) : Repository<Supplier>(context), ISupplierRepository
{
    public Task<bool> CnpjExistsAsync(string cnpj, Guid? ignoreId = null, CancellationToken ct = default) =>
        Set.AnyAsync(s => s.Cnpj == cnpj && s.Id != ignoreId, ct);
}
