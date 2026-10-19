using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class EquipmentRepository(AppDbContext context) : Repository<Equipment>(context), IEquipmentRepository
{
    private IQueryable<Equipment> Detailed => Set.Include(e => e.Address);

    public override Task<Equipment?> GetByIdAsync(Guid id, CancellationToken ct = default) => GetDetailedAsync(id, ct);
    public Task<Equipment?> GetDetailedAsync(Guid id, CancellationToken ct = default) =>
        Detailed.FirstOrDefaultAsync(e => e.Id == id, ct);
    public override Task<List<Equipment>> GetAllAsync(CancellationToken ct = default) =>
        Detailed.AsNoTracking().OrderBy(e => e.SerialNumber).ToListAsync(ct);

    public Task<List<Equipment>> SearchAsync(EquipmentFilter f, CancellationToken ct = default)
    {
        var q = Detailed.AsNoTracking();
        if (f.Type.HasValue) q = q.Where(e => e.Type == f.Type.Value);
        if (f.Status.HasValue) q = q.Where(e => e.Status == f.Status.Value);
        if (!string.IsNullOrWhiteSpace(f.Neighborhood))
        {
            var n = f.Neighborhood.Trim();
            q = q.Where(e => e.Address.Neighborhood == n);
        }
        return q.OrderBy(e => e.SerialNumber).ToListAsync(ct);
    }

    public Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? ignoreId = null, CancellationToken ct = default) =>
        Set.AnyAsync(e => e.SerialNumber == serialNumber && e.Id != ignoreId, ct);
    public Task<bool> LicensePlateExistsAsync(string plate, Guid? ignoreId = null, CancellationToken ct = default) =>
        Context.Equipments.OfType<Vehicle>().AnyAsync(v => v.LicensePlate == plate && v.Id != ignoreId, ct);
    public Task<bool> AgentHasVehicleAsync(Guid agentId, Guid? ignoreVehicleId = null, CancellationToken ct = default) =>
        Context.Equipments.OfType<Vehicle>().AnyAsync(v => v.CurrentAgentId == agentId && v.Id != ignoreVehicleId, ct);
    public async Task<bool> HasDependentsAsync(Guid id, CancellationToken ct = default) =>
        await Context.MaintenanceRecords.AnyAsync(m => m.EquipmentId == id, ct) ||
        await Context.Alerts.AnyAsync(a => a.EquipmentId == id, ct) ||
        await Context.TelemetryLogs.AnyAsync(t => t.EquipmentId == id, ct) ||
        await Context.Incidents.AnyAsync(i => i.EquipmentId == id, ct) ||
        await Context.Recommendations.AnyAsync(r => r.EquipmentId == id, ct);
    public Task<int> CountAsync(CancellationToken ct = default) => Set.CountAsync(ct);
    public async Task<Dictionary<EquipmentStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        (await Set.GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
}
