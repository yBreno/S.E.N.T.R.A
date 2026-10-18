using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class AddressRepository(AppDbContext context) : Repository<Address>(context), IAddressRepository
{
    public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default) =>
        await Context.Equipments.AnyAsync(e => e.AddressId == id, ct) ||
        await Context.OperationalBases.AnyAsync(o => o.AddressId == id, ct);
}
