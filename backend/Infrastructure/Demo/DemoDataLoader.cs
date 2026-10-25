using System.Data;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Demo;

public enum DemoDataProfile
{
    Legacy,
    Evaluation
}
public sealed record DemoLoadResult(int Addresses, int Equipments, int Agents, int Incidents, int Alerts, int Maintenances,
    int Telemetry = 0, int Recommendations = 0)
{
    public int TotalInserted => Addresses + Equipments + Agents + Incidents + Alerts + Maintenances + Telemetry + Recommendations;
}

/// <summary>Explicit manual loader. Not registered in DI or called by API startup.</summary>
public static class DemoDataLoader
{
    public static async Task<DemoLoadResult> ValidateAsync(AppDbContext db, DateTime utcNow,
        string environment, bool confirmed, CancellationToken ct = default,
        Action<string>? targetValidator = null, DemoDataProfile profile = DemoDataProfile.Legacy)
    {
        if (environment != "Demo" || !confirmed)
            throw new InvalidOperationException("Ambiente Demo e confirmacao explicita sao obrigatorios.");
        var sql = db.Database.IsSqlServer();
        if (sql) ValidateTarget(db, environment, targetValidator, profile);
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Provider nao autorizado para DEMO.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Use um contexto exclusivo, sem alteracoes pendentes.");
        var plan = Plan(utcNow, profile);
        if (sql)
        {
            if (!await db.Database.CanConnectAsync(ct))
                throw new InvalidOperationException("Banco isolado indisponivel.");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("Schema pendente. A ferramenta nao executa migrations.");
        }
        await CheckUniqueKeysAsync(db, plan, ct);
        await CheckEvaluationIsolationAsync(db, plan, ct);
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
            (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId && a.AgentId == b.AgentId, ct);
        var telemetry = await MissingAsync(db.TelemetryLogs, plan.Telemetry, (a, b) => a.EquipmentId == b.EquipmentId && a.EventType == b.EventType && a.PayloadData == b.PayloadData, ct);
        var recommendations = await MissingAsync(db.Recommendations, plan.Recommendations, (a, b) => a.EquipmentId == b.EquipmentId && a.Type == b.Type && a.Reason == b.Reason && a.Neighborhood == b.Neighborhood, ct);
        return new(addresses.Count, equipments.Count, agents.Count, incidents.Count, alerts.Count, maintenances.Count, telemetry.Count, recommendations.Count);
    }

    public static async Task<DemoLoadResult> ApplyAsync(AppDbContext db, DateTime utcNow,
        string environment, bool confirmed, CancellationToken ct = default,
        Action<string>? targetValidator = null, DemoDataProfile profile = DemoDataProfile.Legacy)
    {
        if (environment != "Demo" || !confirmed)
            throw new InvalidOperationException("Ambiente Demo e confirmacao explicita sao obrigatorios.");
        var sql = db.Database.IsSqlServer();
        // The only alternative is the existing test provider: never another persistent destination.
        if (sql) ValidateTarget(db, environment, targetValidator, profile);
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Provider nao autorizado para DEMO.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Use um contexto exclusivo, sem alteracoes pendentes.");

        var plan = Plan(utcNow, profile);
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
            await CheckEvaluationIsolationAsync(db, plan, ct);
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
                (a, b) => a.Description == b.Description && a.EquipmentId == b.EquipmentId && a.AgentId == b.AgentId, ct);
            var telemetry = await MissingAsync(db.TelemetryLogs, plan.Telemetry, (a, b) => a.EquipmentId == b.EquipmentId && a.EventType == b.EventType && a.PayloadData == b.PayloadData, ct);
            var recommendations = await MissingAsync(db.Recommendations, plan.Recommendations, (a, b) => a.EquipmentId == b.EquipmentId && a.Type == b.Type && a.Reason == b.Reason && a.Neighborhood == b.Neighborhood, ct);

            db.Addresses.AddRange(addresses);
            db.Equipments.AddRange(equipments);
            db.Agents.AddRange(agents);
            db.Incidents.AddRange(incidents);
            db.Alerts.AddRange(alerts);
            db.MaintenanceRecords.AddRange(maintenances);
            db.TelemetryLogs.AddRange(telemetry);
            db.Recommendations.AddRange(recommendations);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(addresses.Count, equipments.Count, agents.Count, incidents.Count, alerts.Count, maintenances.Count, telemetry.Count, recommendations.Count);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static DemoDataPlan Plan(DateTime at, DemoDataProfile profile) => profile switch
    {
        DemoDataProfile.Legacy => DemoDataPlan.Create(at),
        DemoDataProfile.Evaluation => DemoEvaluationPlan.Create(at),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    private static void ValidateTarget(AppDbContext db, string environment, Action<string>? validator, DemoDataProfile profile)
    {
        var connection = db.Database.GetConnectionString() ?? "";
        if (profile == DemoDataProfile.Evaluation)
        {
            if (validator is not null) throw new InvalidOperationException("Avaliação não admite substituição da proteção de destino.");
            DemoEvaluationTargetGuard.Validate(environment, connection);
        }
        else (validator ?? (value => DemoTargetGuard.Validate(environment, value)))(connection);
    }

    private static async Task CheckEvaluationIsolationAsync(AppDbContext db, DemoDataPlan plan, CancellationToken ct)
    {
        if (plan.Dataset != DemoEvaluationPlan.Prefix) return;
        var reservedDocuments = Enumerable.Range(0, 10).Select(DemoEvaluationPlan.SyntheticDocument).ToArray();
        var telemetryIds = plan.Telemetry.Select(t => t.Id).ToArray();
        if (await db.Equipments.AnyAsync(e => !e.SerialNumber.StartsWith(DemoEvaluationPlan.Prefix + "-"), ct) ||
            await db.Agents.AnyAsync(a => !a.BadgeNumber.StartsWith(DemoEvaluationPlan.Prefix + "-") || !a.Name.StartsWith("DEMO ") || !reservedDocuments.Contains(a.Cpf) || a.UserId != null, ct) ||
            await db.Addresses.AnyAsync(a => a.LocationReference == null || !a.LocationReference.StartsWith(DemoEvaluationPlan.Prefix + "/"), ct) ||
            await db.Users.AnyAsync(u => !u.Name.StartsWith("DEMO ") || !u.Email.EndsWith(".invalid") || !reservedDocuments.Contains(u.Cpf), ct) ||
            await db.Incidents.AnyAsync(i => i.Description == null || !i.Description.StartsWith(DemoEvaluationPlan.Prefix), ct) ||
            await db.Alerts.AnyAsync(a => !a.Message.StartsWith(DemoEvaluationPlan.Prefix), ct) ||
            await db.MaintenanceRecords.AnyAsync(m => !m.Description.StartsWith(DemoEvaluationPlan.Prefix), ct) ||
            await db.Recommendations.AnyAsync(r => !r.Reason.StartsWith(DemoEvaluationPlan.Prefix), ct) ||
            await db.TelemetryLogs.AnyAsync(t => !telemetryIds.Contains(t.Id), ct) ||
            await db.AgentPositionLogs.AnyAsync(ct) || await db.Suppliers.AnyAsync(ct) || await db.OperationalBases.AnyAsync(ct))
            throw new InvalidOperationException("Banco contém outro conjunto de dados; não misture a avaliação com carga existente.");
        foreach (var r in plan.Recommendations)
            if (await db.Recommendations.AnyAsync(x => x.Id != r.Id && x.Status == RecommendationStatus.Pending && x.Type == r.Type && x.EquipmentId == r.EquipmentId &&
                (r.EquipmentId != null || x.Neighborhood == r.Neighborhood), ct))
                throw new InvalidOperationException("Recomendação pendente externa ao plano; nenhuma duplicação permitida.");
        foreach (var m in plan.Maintenances.Where(m => m.Status != MaintenanceStatus.Closed))
            if (await db.MaintenanceRecords.AnyAsync(x => x.Id != m.Id && x.EquipmentId == m.EquipmentId && x.Status != MaintenanceStatus.Closed, ct))
                throw new InvalidOperationException("Ordem ativa externa ao plano; nenhuma duplicação permitida.");
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
