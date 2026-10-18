using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class AgentRepository(AppDbContext context) : Repository<Agent>(context), IAgentRepository
{
    public override Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(a => a.Organization).FirstOrDefaultAsync(a => a.Id == id, ct);
    public override Task<List<Agent>> GetAllAsync(CancellationToken ct = default) =>
        Set.AsNoTracking().Include(a => a.Organization).OrderBy(a => a.Name).ToListAsync(ct);
    public Task<Agent?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Set.Include(a => a.Organization).FirstOrDefaultAsync(a => a.UserId == userId, ct);
    public Task<bool> CpfExistsAsync(string cpf, Guid? ignoreId = null, CancellationToken ct = default) =>
        Set.AnyAsync(a => a.Cpf == cpf && a.Id != ignoreId, ct);
    public Task<bool> BadgeExistsAsync(string badge, Guid? ignoreId = null, CancellationToken ct = default) =>
        Set.AnyAsync(a => a.BadgeNumber == badge && a.Id != ignoreId, ct);
    public Task<bool> UserLinkedAsync(Guid userId, Guid? ignoreAgentId = null, CancellationToken ct = default) =>
        Set.AnyAsync(a => a.UserId == userId && a.Id != ignoreAgentId, ct);
    public async Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default) =>
        await Context.MaintenanceRecords.AnyAsync(m => m.AgentId == id, ct) ||
        await Context.Equipments.OfType<Vehicle>().AnyAsync(v => v.CurrentAgentId == id, ct);
    public Task<int> CountActiveAsync(CancellationToken ct = default) =>
        Set.CountAsync(a => a.Availability != AgentAvailability.Offline, ct);
}
