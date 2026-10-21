using System.Text.Json;
using Application.Common;
using Application.DTOs.Equipment;
using Application.DTOs.Predictive;
using Application.Predictive;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Demo;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Tests;

public class DemoDataTests
{
    private static readonly DateTime At = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SENTRA_DEMO_TEST_{Guid.NewGuid():N}").Options);
    private static Task<DemoLoadResult> Load(AppDbContext db, DateTime? at = null) =>
        DemoDataLoader.ApplyAsync(db, at ?? At, "Demo", confirmed: true);

    [Fact]
    public void Plan_validates_all_DTOs_counts_unique_keys_states_coordinates_and_relationships()
    {
        var plan = DemoDataPlan.Create(At); // Also runs the existing FluentValidation validators.
        Assert.Equal(60, plan.Addresses.Count);
        Assert.Equal(60, plan.Equipments.Count);
        Assert.Equal(20, plan.Agents.Count);
        Assert.Equal(100, plan.Incidents.Count);
        Assert.Equal(25, plan.Incidents.Count(i => i.Type == IncidentType.EquipmentFailure));
        Assert.Equal(15, plan.Alerts.Count);
        Assert.Equal(10, plan.Maintenances.Count);
        Assert.Equal(8, plan.Equipments.Select(e => e.Type).Distinct().Count());
        Assert.All(Enum.GetValues<EquipmentStatus>(), status =>
            Assert.Equal(10, plan.Equipments.Count(e => e.Status == status)));
        Assert.Equal(3, plan.Agents.Select(a => a.Availability).Distinct().Count());
        Assert.Equal(4, plan.Alerts.Select(a => a.Severity).Distinct().Count());
        Assert.Equal(60, plan.Equipments.Select(e => e.SerialNumber).Distinct().Count());
        Assert.Equal(20, plan.Agents.Select(a => a.Cpf).Distinct().Count());
        Assert.Equal(20, plan.Agents.Select(a => a.BadgeNumber).Distinct().Count());
        Assert.Equal(plan.Equipments.OfType<Vehicle>().Count(), plan.Equipments.OfType<Vehicle>().Select(v => v.LicensePlate).Distinct().Count());
        Assert.All(plan.Agents, a => { Assert.Null(a.UserId); Assert.True(Documents.IsValidCpf(a.Cpf)); });
        Assert.All(plan.Addresses.Select(a => (a.Latitude, a.Longitude)).Concat(plan.Agents.Select(a => (a.Latitude, a.Longitude)))
            .Concat(plan.Incidents.Select(a => (a.Latitude, a.Longitude))), p =>
        {
            Assert.InRange(p.Latitude, -23.61, -23.53);
            Assert.InRange(p.Longitude, -46.71, -46.57);
        });
        Assert.All(plan.Incidents, i => Assert.InRange(i.OccurredAt, At.AddDays(-7), At));
        Assert.All(plan.Alerts, a => Assert.InRange(a.CreatedAt, At.AddDays(-30), At));
        Assert.All(plan.Incidents.Where(i => i.Type == IncidentType.EquipmentFailure), i =>
        {
            var eq = Assert.Single(plan.Equipments, e => e.Id == i.EquipmentId);
            Assert.Equal(EquipmentStatus.Malfunctioning, eq.Status);
            var address = Assert.Single(plan.Addresses, a => a.Id == eq.AddressId);
            Assert.Equal(address.Neighborhood, i.Neighborhood);
            Assert.Equal(address.Latitude, i.Latitude);
            Assert.Equal(address.Longitude, i.Longitude);
        });
        Assert.All(plan.Equipments.Where(e => e.Status == EquipmentStatus.UnderMaintenance), e =>
            Assert.Single(plan.Maintenances, m => m.EquipmentId == e.Id && m.Status == MaintenanceStatus.Open));
        Assert.Equal(5, plan.Incidents.Select(i => i.Neighborhood).Distinct().Count());
    }

    [Fact]
    public void Identities_are_stable_but_new_plan_dates_are_relative_to_execution()
    {
        var first = DemoDataPlan.Create(At);
        var next = DemoDataPlan.Create(At.AddDays(10));
        Assert.Equal(first.Equipments.Select(e => e.Id), next.Equipments.Select(e => e.Id));
        Assert.Equal(first.Incidents.Select(e => e.Id), next.Incidents.Select(e => e.Id));
        Assert.Equal(first.Incidents[0].OccurredAt.AddDays(10), next.Incidents[0].OccurredAt);
        Assert.Throws<ArgumentException>(() => DemoDataPlan.Create(DateTime.SpecifyKind(At, DateTimeKind.Unspecified)));
    }

    [Fact]
    public async Task Repetition_is_noop_even_with_later_clock_and_preserves_every_existing_scalar()
    {
        await using var db = NewDb();
        var first = await Load(db);
        Assert.Equal(265, first.TotalInserted);
        // Existing DEMO records can have legitimate subsequent changes: never reset them.
        (await db.Equipments.FirstAsync()).Brand = "DEMO ajustado";
        await db.SaveChangesAsync();
        var before = Snapshot(db);
        var second = await Load(db, At.AddDays(10));
        Assert.Equal(0, second.TotalInserted);
        Assert.Equal(before, Snapshot(db));
    }

    [Fact]
    public async Task Separate_contexts_reuse_persisted_identities_and_do_not_refresh_expired_incidents()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SENTRA_DEMO_TEST_{Guid.NewGuid():N}").Options;
        await using (var first = new AppDbContext(options)) Assert.Equal(265, (await Load(first)).TotalInserted);
        await using var second = new AppDbContext(options);
        Assert.Equal(0, (await Load(second, At.AddDays(10))).TotalInserted);
        var settings = Options.Create(new PredictiveSettings());
        var clock = new FixedClock(At.AddDays(10));
        var risk = new RiskService(new IncidentRepository(second), new AlertRepository(second),
            new EquipmentRepository(second), settings, clock);
        var heatmap = new HeatmapService(new IncidentRepository(second), risk, settings, clock);
        Assert.Equal(0, (await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Incidents })).TotalPoints);
        Assert.Equal(0, (await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Failures })).TotalPoints);
        Assert.Equal(5, (await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Risk })).TotalPoints);
        Assert.Equal(At.AddMinutes(-75), (await second.Incidents.OrderByDescending(i => i.OccurredAt).FirstAsync()).OccurredAt);
    }

    [Fact]
    public async Task Partial_seed_is_completed_without_requiring_empty_incidents_or_modifying_unrelated_rows()
    {
        await using var db = NewDb();
        var unrelated = new Incident
        {
            Neighborhood = "Outro bairro", Latitude = -23.5, Longitude = -46.6,
            Description = "Fixture externa a carga DEMO", OccurredAt = At.AddDays(-90)
        };
        db.Incidents.Add(unrelated);
        var plan = DemoDataPlan.Create(At);
        db.Addresses.Add(plan.Addresses[0]);
        db.Equipments.Add(plan.Equipments[0]);
        db.Agents.Add(plan.Agents[0]);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(unrelated);
        Assert.Equal(262, (await Load(db)).TotalInserted);
        Assert.Equal(101, await db.Incidents.CountAsync());
        Assert.Equal(before, JsonSerializer.Serialize(await db.Incidents.AsNoTracking().SingleAsync(i => i.Id == unrelated.Id)));
        Assert.Equal(0, (await Load(db)).TotalInserted);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.TelemetryLogs.ToListAsync());
        Assert.Empty(await db.Recommendations.ToListAsync());
    }

    [Theory]
    [InlineData("serial")]
    [InlineData("plate")]
    [InlineData("badge")]
    [InlineData("cpf")]
    [InlineData("id")]
    [InlineData("relationship")]
    public async Task Conflicts_abort_before_inserts_and_never_overwrite(string conflict)
    {
        await using var db = NewDb();
        var plan = DemoDataPlan.Create(At);
        if (conflict is "serial" or "plate")
            db.Equipments.Add(new Vehicle
            {
                SerialNumber = conflict == "serial" ? plan.Equipments[0].SerialNumber : "EXTERNAL",
                LicensePlate = conflict == "plate" ? plan.Equipments.OfType<Vehicle>().First().LicensePlate : "EXT1234"
            });
        else if (conflict is "badge" or "cpf")
            db.Agents.Add(new Agent
            {
                Name = "Fixture externa", BadgeNumber = conflict == "badge" ? plan.Agents[0].BadgeNumber : "EXTERNAL",
                Cpf = conflict == "cpf" ? plan.Agents[0].Cpf : "external"
            });
        else if (conflict == "id")
            db.Addresses.Add(new Address { Id = plan.Addresses[0].Id, LocationReference = "EXTERNAL" });
        else
        {
            var incident = plan.Incidents[0];
            incident.EquipmentId = Guid.NewGuid();
            db.Incidents.Add(incident);
        }
        await db.SaveChangesAsync();
        var before = Snapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Load(db));
        Assert.Equal(before, Snapshot(db));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Existing_services_return_equipment_agent_DTOs_and_all_three_real_heatmap_calculations()
    {
        await using var db = NewDb();
        await Load(db);
        db.ChangeTracker.Clear();
        var equipments = new EquipmentRepository(db);
        var agents = new AgentRepository(db);
        var options = Options.Create(new PredictiveSettings());
        var clock = new FixedClock(At);
        var risk = new RiskService(new IncidentRepository(db), new AlertRepository(db), equipments, options, clock);
        var heatmap = new HeatmapService(new IncidentRepository(db), risk, options, clock);
        var eqService = new EquipmentService(equipments, new AddressRepository(db), agents,
            new AlertRepository(db), new MaintenanceRecordRepository(db), db,
            new CreateEquipmentDtoValidator(), new UpdateEquipmentDtoValidator(), new ChangeEquipmentStatusDtoValidator());
        var agentService = new AgentService(agents, new UserRepository(db), db,
            new CreateAgentDtoValidator(), new UpdateAgentDtoValidator());
        var equipmentDtos = await eqService.SearchAsync(new EquipmentSearchDto(null, null, null));
        var agentDtos = await agentService.GetAllAsync();
        Assert.Equal(60, equipmentDtos.Count);
        Assert.Equal(20, agentDtos.Count);
        Assert.All(equipmentDtos, e => { Assert.NotNull(e.Neighborhood); Assert.NotNull(e.Latitude); Assert.NotNull(e.Longitude); });
        Assert.All(agentDtos, a => Assert.True(Documents.IsValidLatitude(a.Latitude) && Documents.IsValidLongitude(a.Longitude)));
        var incidents = await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Incidents });
        var failures = await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Failures });
        var regionalRisk = await heatmap.GetAsync(new HeatmapQueryDto { Metric = HeatmapMetric.Risk });
        Assert.Equal(100, incidents.TotalPoints);
        Assert.Equal(25, failures.TotalPoints);
        Assert.Equal(5, regionalRisk.TotalPoints);
        Assert.All(incidents.Points.Concat(failures.Points).Concat(regionalRisk.Points), p =>
        {
            Assert.InRange(p.Intensity, 0.01, 1);
            Assert.True(Documents.IsValidLatitude(p.Latitude) && Documents.IsValidLongitude(p.Longitude));
        });
        Assert.All(await risk.GetAllAsync(), r => Assert.InRange(r.Score, 1, 100));
        Assert.Equal(13, await agents.CountActiveAsync());
        Assert.Equal(10, (await equipments.CountByStatusAsync())[EquipmentStatus.Operational]);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", true)]
    [InlineData("Demo", false)]
    public async Task Environment_and_confirmation_are_required(string environment, bool confirmed)
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DemoDataLoader.ApplyAsync(db, At, environment, confirmed));
        Assert.Empty(await db.Addresses.ToListAsync());
    }

    [Fact]
    public async Task Unrelated_pending_changes_are_rejected_without_being_saved_or_detached()
    {
        await using var db = NewDb();
        db.Agents.Add(new Agent { Name = "Fixture pendente" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Load(db));
        Assert.True(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.Agents.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Production", @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Test;Integrated Security=true")]
    [InlineData("Demo", "Server=example.database.windows.net;Database=SENTRA_DEMO_Test;Integrated Security=true")]
    [InlineData("Demo", "Server=localhost;Database=SENTRA_DEMO_Test;Integrated Security=true")]
    [InlineData("Demo", @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA;Integrated Security=true")]
    [InlineData("Demo", @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true")]
    [InlineData("Demo", @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Test;Integrated Security=false")]
    public void Remote_ambiguous_or_non_demo_targets_are_rejected_before_connection(string environment, string connection)
    {
        Assert.Throws<InvalidOperationException>(() => DemoTargetGuard.Validate(environment, connection));
    }

    [Fact]
    public void Explicit_local_demo_target_is_accepted_without_connecting()
    {
        const string cs = @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Test;Integrated Security=true";
        DemoTargetGuard.Validate("Demo", cs, expectedDatabase: "SENTRA_DEMO_Test");
        Assert.Throws<InvalidOperationException>(() => DemoTargetGuard.Validate("Demo", cs, expectedDatabase: "master"));
    }

    private static string Snapshot(AppDbContext db)
    {
        // Scalar snapshots include every mapped field, excluding navigations and tracking state.
        db.Addresses.Load(); db.Equipments.Load(); db.Agents.Load(); db.Incidents.Load(); db.Alerts.Load(); db.MaintenanceRecords.Load();
        return JsonSerializer.Serialize(db.ChangeTracker.Entries().OrderBy(e => ((BaseEntity)e.Entity).Id)
            .Select(e => e.Properties.OrderBy(p => p.Metadata.Name).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)));
    }

    private sealed class FixedClock(DateTime at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(at);
    }
}
