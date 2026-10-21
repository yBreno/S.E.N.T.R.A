using System.Data;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Demo;

public sealed record DemoLoadResult(int Addresses, int Equipments, int Agents, int Incidents, int Alerts, int Maintenances)
{
    public int TotalInserted => Addresses + Equipments + Agents + Incidents + Alerts + Maintenances;
}

/// <summary>Explicit manual loader. Not registered in DI or called by API startup.</summary>
public static class DemoDataLoader
{
    public static async Task<DemoLoadResult> ValidateAsync(AppDbContext db, DateTime utcNow,
        string environment, bool confirmed, CancellationToken ct = default,
        Action<string>? targetValidator = null)
    {
        if (environment != "Demo" || !confirmed)
            throw new InvalidOperationException("Ambiente Demo e confirmacao explicita sao obrigatorios.");
        var sql = db.Database.IsSqlServer();
        if (sql) (targetValidator ?? (connection => DemoTargetGuard.Validate(environment, connection)))(db.Database.GetConnectionString() ?? "");
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Provider nao autorizado para DEMO.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Use um contexto exclusivo, sem alteracoes pendentes.");
        var plan = DemoDataPlan.Create(utcNow);
        if (sql)
        {
            if (!await db.Database.CanConnectAsync(ct))
                throw new InvalidOperationException("Banco isolado indisponivel.");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("Schema pendente. A ferramenta nao executa migrations.");
        }
        await CheckUniqueKeysAsync(db, plan, ct);
        var addresses = await MissingAsync(db.Addresses, plan.Addresses,
            (a, b) => a.LocationReference == b.LocationReference, ct);
        var equipments = await MissingAsync(db.Equipments, plan.Equipments,
            (a, b) => a.SerialNumber == b.SerialNumber && a.AddressId == b.AddressId && a.Type == b.Type &&
                (a as Vehicle)?.LicensePlate == (b as Vehicle)?.LicensePlate, ct);
        var agents = await MissingAsync(db.Agents, plan.Agents,
            (a, b) => a.BadgeNumber == b.BadgeNumber && a.Cpf == b.Cpf && a.Name.StartsWith("DEMO ") && a.UserId == null, ct);
        var incidents = await MissingAsync(db.Incidents, plan.Incidents,
            (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId, ct);
        var alerts = await MissingAsync(db.Alerts, plan.Alerts,
            (a, b) => a.Message == b.Message && a.EquipmentId == b.EquipmentId, ct);
        var maintenances = await MissingAsync(db.MaintenanceRecords, plan.Maintenances,
            (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId && a.AgentId == null, ct);
        return new(addresses.Count, equipments.Count, agents.Count, incidents.Count, alerts.Count, maintenances.Count);
    }

    public static async Task<DemoLoadResult> ApplyAsync(AppDbContext db, DateTime utcNow,
        string environment, bool confirmed, CancellationToken ct = default,
        Action<string>? targetValidator = null)
    {
        if (environment != "Demo" || !confirmed)
            throw new InvalidOperationException("Ambiente Demo e confirmacao explicita sao obrigatorios.");
        var sql = db.Database.IsSqlServer();
        // The only alternative is the existing test provider: never another persistent destination.
        if (sql) (targetValidator ?? (connection => DemoTargetGuard.Validate(environment, connection)))(db.Database.GetConnectionString() ?? "");
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Provider nao autorizado para DEMO.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Use um contexto exclusivo, sem alteracoes pendentes.");

        var plan = DemoDataPlan.Create(utcNow);
        if (sql)
        {
            if (!await db.Database.CanConnectAsync(ct))
                throw new InvalidOperationException("Banco local isolado indisponivel. Nenhum banco ou schema sera criado.");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("Schema local pendente. A ferramenta nao executa migrations.");
        }
        await using var transaction = sql
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            // Check ALL identities and unique business keys before tracking any insert.
            await CheckUniqueKeysAsync(db, plan, ct);
            var addresses = await MissingAsync(db.Addresses, plan.Addresses,
                (a, b) => a.LocationReference == b.LocationReference, ct);
            var equipments = await MissingAsync(db.Equipments, plan.Equipments,
                (a, b) => a.SerialNumber == b.SerialNumber && a.AddressId == b.AddressId && a.Type == b.Type &&
                    (a as Vehicle)?.LicensePlate == (b as Vehicle)?.LicensePlate, ct);
            var agents = await MissingAsync(db.Agents, plan.Agents,
                (a, b) => a.BadgeNumber == b.BadgeNumber && a.Cpf == b.Cpf && a.Name.StartsWith("DEMO ") && a.UserId == null, ct);
            var incidents = await MissingAsync(db.Incidents, plan.Incidents,
                (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId, ct);
            var alerts = await MissingAsync(db.Alerts, plan.Alerts,
                (a, b) => a.Message == b.Message && a.EquipmentId == b.EquipmentId, ct);
            var maintenances = await MissingAsync(db.MaintenanceRecords, plan.Maintenances,
                (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId && a.AgentId == null, ct);

            db.Addresses.AddRange(addresses);
            db.Equipments.AddRange(equipments);
            db.Agents.AddRange(agents);
            db.Incidents.AddRange(incidents);
            db.Alerts.AddRange(alerts);
            db.MaintenanceRecords.AddRange(maintenances);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(addresses.Count, equipments.Count, agents.Count, incidents.Count, alerts.Count, maintenances.Count);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static async Task<List<T>> MissingAsync<T>(DbSet<T> set, IReadOnlyList<T> planned,
        Func<T, T, bool> sameIdentity, CancellationToken ct) where T : BaseEntity
    {
        var ids = planned.Select(x => x.Id).ToArray();
        var existing = await set.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var missing = new List<T>();
        foreach (var item in planned)
        {
            if (!existing.TryGetValue(item.Id, out var stored)) missing.Add(item);
            else if (!sameIdentity(stored, item))
                throw new InvalidOperationException($"Conflito de identidade DEMO em {typeof(T).Name}; nenhuma sobrescrita permitida.");
        }
        return missing;
    }

    private static async Task CheckUniqueKeysAsync(AppDbContext db, DemoDataPlan plan, CancellationToken ct)
    {
        var serials = plan.Equipments.Select(e => e.SerialNumber).ToArray();
        var plates = plan.Equipments.OfType<Vehicle>().Select(e => e.LicensePlate).ToArray();
        var existingEquipments = await db.Equipments.AsNoTracking().Where(e => serials.Contains(e.SerialNumber)).ToListAsync(ct);
        var existingVehicles = await db.Equipments.OfType<Vehicle>().AsNoTracking().Where(e => plates.Contains(e.LicensePlate)).ToListAsync(ct);
        if (existingEquipments.Any(e => !plan.Equipments.Any(p => p.Id == e.Id && p.SerialNumber == e.SerialNumber)) ||
            existingVehicles.Any(e => !plan.Equipments.OfType<Vehicle>().Any(p => p.Id == e.Id && p.LicensePlate == e.LicensePlate)))
            throw new InvalidOperationException("Conflito de serial ou placa DEMO. Nenhuma alteracao aplicada.");
        var cpfs = plan.Agents.Select(a => a.Cpf).ToArray();
        var badges = plan.Agents.Select(a => a.BadgeNumber).ToArray();
        var agents = await db.Agents.AsNoTracking().Where(a => cpfs.Contains(a.Cpf) || badges.Contains(a.BadgeNumber)).ToListAsync(ct);
        if (agents.Any(a => !plan.Agents.Any(p => p.Id == a.Id && p.Cpf == a.Cpf && p.BadgeNumber == a.BadgeNumber)))
            throw new InvalidOperationException("Conflito de documento sintetico ou matricula DEMO. Nenhuma alteracao aplicada.");
    }
}
