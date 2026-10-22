using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common;
using Application.DTOs.Equipment;
using Application.DTOs.Predictive;
using Application.Predictive;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Demo;
using Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

// Manual Stage 7.3 runner: no API reference, startup, application configuration, users or worker.
const string target = "SENTRA_DEMO_Stage73";
string[] migrations = ["20261018170813_InitialCreate", "20261018220253_RemoveOrganization", "20261019031247_AddPredictiveCity"];
string[] tables = ["Addresses", "Equipments", "Agents", "Incidents", "Alerts", "MaintenanceRecords",
    "Users", "TelemetryLogs", "Recommendations", "AgentPositionLogs", "Suppliers", "OperationalBases"];
var phase = "arguments";
try
{
    if (args is not ["--prepare-new", "--confirm", target] && args is not ["--inspect"] &&
        args is not ["--verify", "--expected-fingerprint", _, "--confirm", target])
        throw new InvalidOperationException("Explicit Stage73 operation required.");
    var cs = Environment.GetEnvironmentVariable("SENTRA_DEMO_CONNECTION_STRING") ?? "";
    DemoTargetGuard.Validate(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), cs, target);
    var asp = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    Require(asp is null or "Demo", "Unexpected ASP.NET environment.");
    AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);

    if (args[0] == "--prepare-new")
    {
        phase = "confirm-new-local-target";
        await using var db = NewContext();
        Require(db.Database.GetMigrations().SequenceEqual(migrations), "Migration manifest changed; stop.");
        // Query and CREATE are in one server batch. If another process creates it, abort rather than adopt it.
        var masterSettings = new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" };
        await using var master = new SqlConnection(masterSettings.ConnectionString);
        await master.OpenAsync();
        await using (var command = master.CreateCommand())
        {
            command.CommandText = "IF ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')),0) <> 1 THROW 51000, 'LocalDB required', 1; " +
                "IF DB_ID(N'SENTRA_DEMO_Stage73') IS NOT NULL THROW 51001, 'Target already exists: stop before modification', 1; " +
                "CREATE DATABASE [SENTRA_DEMO_Stage73];";
            await command.ExecuteNonQueryAsync();
        }
        phase = "existing-migrations-only";
        await ConfirmTarget(db);
        await db.GetService<IMigrator>().MigrateAsync(migrations[^1]);
        Require((await db.Database.GetAppliedMigrationsAsync()).SequenceEqual(migrations), "Migration history mismatch.");
        phase = "new-schema-snapshot";
        var snapshot = await Snapshot(db);
        Require(snapshot.Counts.Values.All(n => n == 0), "New schema not empty.");
        Output(new { operation = "prepare-new", database = target, migrations, snapshot.Counts, utc = DateTime.UtcNow });
        return 0;
    }

    phase = "read-persistent-state";
    await using var context = NewContext();
    await ConfirmTarget(context);
    Require((await context.Database.GetAppliedMigrationsAsync()).SequenceEqual(migrations), "Migration history mismatch.");
    Require(!(await context.Database.GetPendingMigrationsAsync()).Any(), "Unexpected pending migrations.");
    var before = await Snapshot(context);
    if (args[0] == "--inspect")
    {
        Output(new { operation = "inspect", database = target, before.Counts, before.Fingerprint, before.OperationalFingerprint, utc = DateTime.UtcNow });
        return 0;
    }
    Require(before.Fingerprint == args[2], "Persistent values changed after first load.");
    Require(before.Counts["Addresses"] == 60 && before.Counts["Equipments"] == 60 && before.Counts["Agents"] == 20 &&
        before.Counts["Incidents"] == 100 && before.Counts["Alerts"] == 15 && before.Counts["MaintenanceRecords"] == 10,
        "DEMO counts mismatch.");
    Require(tables.Skip(6).All(t => before.Counts[t] == 0), "Unexpected unrelated data.");

    phase = "constraints-and-relationships";
    await using (var command = context.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 0, "Disabled/untrusted foreign key.");
        command.CommandText = "DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS";
        await using var reader = await command.ExecuteReaderAsync();
        Require(!await reader.ReadAsync(), "Relational constraint violation found.");
    }
    var probeResults = new Dictionary<string, int>();
    var equipment = await context.Equipments.AsNoTracking().FirstAsync();
    var address = await context.Addresses.AsNoTracking().FirstAsync();
    var agent = await context.Agents.AsNoTracking().FirstAsync();
    var vehicle = await context.Equipments.OfType<Vehicle>().AsNoTracking().FirstAsync();
    await Probe("unique-serial", new Camera { SerialNumber = equipment.SerialNumber, Brand = "DEMO", Model = "probe", AddressId = address.Id }, [2601, 2627]);
    await Probe("unique-plate", new Vehicle { SerialNumber = "STAGE73-PROBE-PLATE", Brand = "DEMO", Model = "probe", AddressId = address.Id, LicensePlate = vehicle.LicensePlate }, [2601, 2627]);
    await Probe("unique-badge", new Agent { Name = "DEMO probe", Cpf = "00000000000", BadgeNumber = agent.BadgeNumber, Neighborhood = "Centro" }, [2601, 2627]);
    await Probe("unique-synthetic-document", new Agent { Name = "DEMO probe", Cpf = agent.Cpf, BadgeNumber = "STAGE73-PROBE", Neighborhood = "Centro" }, [2601, 2627]);
    await Probe("foreign-key", new Incident { Neighborhood = "Centro", Description = "DEMO rollback-only probe", EquipmentId = Guid.NewGuid() }, [547]);
    await Probe("not-null", new Address { Street = null!, Neighborhood = "Centro", City = "DEMO", ZipCode = "00000000" }, [515]);
    await Probe("maximum-length", new Alert { EquipmentId = equipment.Id, Message = new string('X', 501) }, [2628, 8152]);

    phase = "explicit-transaction-rollback";
    var rollbackId = Guid.NewGuid();
    await using (var db = NewContext())
    {
        await ConfirmTarget(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Addresses.Add(new Address { Id = rollbackId, Street = "DEMO rollback-only probe", Neighborhood = "Centro", City = "DEMO", ZipCode = "00000000" });
        await db.SaveChangesAsync();
        Require(await db.Addresses.AsNoTracking().AnyAsync(a => a.Id == rollbackId), "Probe insert not visible in transaction.");
        await transaction.RollbackAsync();
    }
    Require(!await context.Addresses.AsNoTracking().AnyAsync(a => a.Id == rollbackId), "Rollback failed.");

    phase = "existing-services-against-sql";
    var eqRepo = new EquipmentRepository(context);
    var agentsRepo = new AgentRepository(context);
    var incidentsRepo = new IncidentRepository(context);
    var alertsRepo = new AlertRepository(context);
    var settings = Options.Create(new PredictiveSettings());
    var risk = new RiskService(incidentsRepo, alertsRepo, eqRepo, settings, TimeProvider.System);
    var heatmap = new HeatmapService(incidentsRepo, risk, settings, TimeProvider.System);
    var equipmentService = new EquipmentService(eqRepo, new AddressRepository(context), agentsRepo,
        alertsRepo, new MaintenanceRecordRepository(context), context,
        new CreateEquipmentDtoValidator(), new UpdateEquipmentDtoValidator(), new ChangeEquipmentStatusDtoValidator());
    var agentService = new AgentService(agentsRepo, new UserRepository(context), context,
        new CreateAgentDtoValidator(), new UpdateAgentDtoValidator());
    var eqDtos = await equipmentService.SearchAsync(new EquipmentSearchDto(null, null, null));
    var agentDtos = await agentService.GetAllAsync();
    Require(eqDtos.Count == 60 && agentDtos.Count == 20, "Service record count mismatch.");
    Require(eqDtos.All(e => e.Latitude.HasValue && e.Longitude.HasValue && Documents.IsValidLatitude(e.Latitude.Value) &&
        Documents.IsValidLongitude(e.Longitude.Value) && !string.IsNullOrWhiteSpace(e.Neighborhood)), "Equipment coordinates invalid.");
    Require(agentDtos.All(a => a.UserId is null && Documents.IsValidLatitude(a.Latitude) && Documents.IsValidLongitude(a.Longitude)), "Agent data invalid.");
    var metricResults = new List<object>();
    foreach (var metric in Enum.GetValues<HeatmapMetric>())
    {
        var data = await heatmap.GetAsync(new HeatmapQueryDto { Metric = metric });
        var expected = metric switch { HeatmapMetric.Incidents => 100, HeatmapMetric.Failures => 25, _ => 5 };
        Require(data.TotalPoints == expected && data.Points.Count == expected, "Heatmap count mismatch.");
        Require(data.Points.All(p => Documents.IsValidLatitude(p.Latitude) && Documents.IsValidLongitude(p.Longitude)
            && double.IsFinite(p.Intensity) && p.Intensity > 0 && p.Intensity <= 1), "Heatmap point invalid.");
        metricResults.Add(new { metric = metric.ToString(), data.TotalPoints, data.From, data.To, data.MaxIntensity, validCoordinates = data.Points.Count });
    }
    var scores = await risk.GetAllAsync();
    Require(scores.Count == 5 && scores.All(r => double.IsFinite(r.Score) && r.Score is > 0 and <= 100), "Risk scores invalid.");
    var activeAgents = await agentsRepo.CountActiveAsync();
    var byStatus = await eqRepo.CountByStatusAsync();
    Require(activeAgents == 13 && byStatus.Count == 6 && byStatus.Values.All(n => n == 10), "Operational indicators mismatch.");
    var invalidMaintenance = await context.Equipments.Where(e => e.Status == Domain.Enums.EquipmentStatus.UnderMaintenance)
        .CountAsync(e => !context.MaintenanceRecords.Any(m => m.EquipmentId == e.Id && m.Status == Domain.Enums.MaintenanceStatus.Open));
    Require(invalidMaintenance == 0, "Maintenance relationship inconsistent.");
    var after = await Snapshot(context);
    Require(before.Fingerprint == after.Fingerprint, "Validation altered persisted data.");
    Output(new
    {
        operation = "verify", database = target, after.Counts, totalDemoRecords = after.Counts.Values.Sum(),
        idempotencyFingerprintUnchanged = true, allPersistedValuesPreserved = true,
        constraintViolations = 0, expectedRejectedProbes = probeResults, rollbackConfirmed = true,
        services = new { equipments = eqDtos.Count, agents = agentDtos.Count, activeAgents, availabilityPercent = Math.Round(10 * 100.0 / 60, 2) },
        heatmap = metricResults, risk = scores.Select(r => new { r.Neighborhood, r.Score, level = r.Level.ToString() }), utc = DateTime.UtcNow
    });
    return 0;

    async Task Probe(string name, BaseEntity entity, int[] expectedNumbers)
    {
        await using var db = NewContext();
        await ConfirmTarget(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Add(entity);
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Constraint did not reject probe.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && expectedNumbers.Contains(sql.Number))
        {
            probeResults.Add(name, sql.Number); // Do not output provider messages (can include document values).
        }
        finally { await transaction.RollbackAsync(); }
    }

    async Task<(Dictionary<string, int> Counts, string Fingerprint, string OperationalFingerprint)> Snapshot(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        var counts = new Dictionary<string, int>();
        var all = new StringBuilder();
        var operational = new StringBuilder();
        foreach (var table in tables.Append("__EFMigrationsHistory"))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [dbo].[{table}]";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (table != "__EFMigrationsHistory") counts.Add(table, count);
            var key = table == "__EFMigrationsHistory" ? "MigrationId" : "Id";
            command.CommandText = $"SELECT COALESCE(CAST((SELECT * FROM [dbo].[{table}] ORDER BY [{key}] FOR JSON PATH, INCLUDE_NULL_VALUES) AS nvarchar(max)), N'[]')";
            var rows = Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            all.Append(table).Append(rows);
            if (table != "Users") operational.Append(table).Append(rows);
        }
        return (counts, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString()))),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operational.ToString()))));
    }

    async Task ConfirmTarget(AppDbContext db)
    {
        DemoTargetGuard.Validate("Demo", db.Database.GetConnectionString() ?? "", target);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CASE WHEN CONVERT(int, SERVERPROPERTY('IsLocalDB'))=1 AND DB_NAME()=N'SENTRA_DEMO_Stage73' THEN 1 ELSE 0 END";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "Server did not confirm exact LocalDB database.");
    }
}
catch (Exception ex)
{
    var sql = ex as SqlException ?? ex.InnerException as SqlException;
    Output(new { operation = "failed", phase, errorType = ex.GetType().Name, sqlError = sql?.Number, utc = DateTime.UtcNow });
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Output(object result) => Console.WriteLine(JsonSerializer.Serialize(result));
