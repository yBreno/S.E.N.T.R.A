using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class OrganizationRepository(AppDbContext context) : Repository<Organization>(context), IOrganizationRepository
{
    public Task<bool> NameExistsAsync(string name, Guid? ignoreId = null, CancellationToken ct = default) =>
        Set.AnyAsync(x => x.Name == name && x.Id != ignoreId, ct);
    public async Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default) =>
        await Context.Agents.AnyAsync(a => a.OrganizationId == id, ct) ||
        await Context.Equipments.AnyAsync(e => e.OrganizationId == id, ct);
}
