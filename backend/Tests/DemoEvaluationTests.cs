using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.Predictive;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
namespace Tests;

public class DemoEvaluationTests
{
    private static readonly DateTime At = new(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc);
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Evaluation_{Guid.NewGuid():N}").Options);
    private static Task<DemoLoadResult> Load(AppDbContext db, DateTime? at = null) => DemoDataLoader.ApplyAsync(db, at ?? At, "Demo", true, profile: DemoDataProfile.Evaluation);

    [Fact]
    public void Plano_reproduzivel_tem_cinco_cenarios_e_documentos_inequivocamente_ficticios()
    {
        var plan = DemoEvaluationPlan.Create(At); var next = DemoEvaluationPlan.Create(At.AddDays(3));
        Assert.Equal(12, plan.Equipments.Count); Assert.Equal(12, plan.Addresses.Count); Assert.Equal(9, plan.Agents.Count);
        Assert.Equal(19, plan.Incidents.Count); Assert.Equal(8, plan.Alerts.Count); Assert.Equal(9, plan.Maintenances.Count);
        Assert.Equal(8, plan.Telemetry.Count); Assert.Equal(2, plan.Recommendations.Count); Assert.Equal(8, plan.Equipments.Select(e => e.Type).Distinct().Count());
        Assert.Equal(5, plan.Addresses.Select(a => a.Neighborhood).Distinct().Count());
        Assert.Equal(plan.Equipments.Select(e => e.Id), next.Equipments.Select(e => e.Id));
        Assert.Equal(plan.Incidents[0].OccurredAt.AddDays(3), next.Incidents[0].OccurredAt);
        Assert.All(plan.Agents, agent => { Assert.False(Documents.IsValidCpf(agent.Cpf)); Assert.True(DemoEvaluationPlan.IsSyntheticDocument(agent.Cpf)); Assert.StartsWith("DEMO ", agent.Name); });
        Assert.All(plan.Telemetry, item => Assert.True(JsonDocument.Parse(item.PayloadData!).RootElement.GetProperty("synthetic").GetBoolean()));
        Assert.All(plan.Addresses, a => { Assert.Equal("São Paulo", a.City); Assert.Equal("00000000", a.ZipCode); Assert.Contains("DEMO", a.Street); Assert.InRange(a.Latitude, -23.61, -23.53); Assert.InRange(a.Longitude, -46.71, -46.57); });
        Assert.Throws<ArgumentException>(() => DemoEvaluationPlan.Create(DateTime.SpecifyKind(At, DateTimeKind.Unspecified)));
    }
    [Fact]
    public void Falhas_resolvidas_ordens_estados_e_datas_formam_historias_consistentes()
    {
        var plan = DemoEvaluationPlan.Create(At);
        foreach (var order in plan.Maintenances)
        {
            var equipment = plan.Equipments.Single(e => e.Id == order.EquipmentId);
            if (order.Status == MaintenanceStatus.Scheduled) { Assert.Null(order.InitDate); Assert.Null(order.EndDate); Assert.True(order.ScheduledDate > At); Assert.Equal(order.ScheduledDate, equipment.NextMaintenanceDate); }
            if (order.Status == MaintenanceStatus.Open) { Assert.Equal(EquipmentStatus.UnderMaintenance, equipment.Status); Assert.True(order.InitDate < At); Assert.Null(order.EndDate); }
            if (order.Status == MaintenanceStatus.Closed) { Assert.True(order.InitDate < order.EndDate); Assert.True(order.EndDate <= At); }
        }
        Assert.All(plan.Equipments.Where(e => e.Status == EquipmentStatus.UnderMaintenance), e => Assert.Single(plan.Maintenances, m => m.EquipmentId == e.Id && m.Status == MaintenanceStatus.Open));
        Assert.All(plan.Maintenances.Where(m => m.Status != MaintenanceStatus.Closed).GroupBy(m => m.EquipmentId), g => Assert.Single(g));
        foreach (var alert in plan.Alerts.Where(a => a.Type == AlertType.Failure))
        {
            Assert.Contains(plan.Incidents, i => i.EquipmentId == alert.EquipmentId && i.Type == IncidentType.EquipmentFailure && i.OccurredAt == alert.CreatedAt);
            if (alert.Status == AlertStatus.Resolved) Assert.Contains(plan.Maintenances, m => m.EquipmentId == alert.EquipmentId && m.Status == MaintenanceStatus.Closed && m.EndDate == alert.ResolvedAt);
        }
    }
    [Fact]
    public void Indices_e_recomendacoes_derivam_das_evidencias_sem_simular_quebra_da_camera()
    {
        var plan = DemoEvaluationPlan.Create(At);
        foreach (var e in plan.Equipments) e.Address = plan.Addresses.Single(a => a.Id == e.AddressId);
        var assessment = EquipmentRiskCalculator.Calculate(At, plan.Equipments, plan.Alerts, plan.Maintenances, plan.Telemetry).Single(r => r.EquipmentId == DemoEvaluationPlan.Id("equipment", 0));
        Assert.Equal(EquipmentStatus.Operational, assessment.Status); Assert.Equal(85, assessment.Score); Assert.Equal(RiskLevel.High, assessment.Level);
        Assert.DoesNotContain(plan.Maintenances, m => m.EquipmentId == assessment.EquipmentId && m.Status != MaintenanceStatus.Closed);
        var rec = plan.Recommendations.Single(r => r.Id == DemoEvaluationPlan.PreventiveRecommendationId);
        Assert.Equal(assessment.Score, rec.RiskScore);
        Assert.Equal(assessment.Score, JsonDocument.Parse(rec.ExplanationJson).RootElement.GetProperty("assessment").GetProperty("score").GetDouble());
        var regional = RiskCalculator.Calculate(new(At, plan.Incidents, plan.Alerts, plan.Equipments, plan.Agents), new());
        Assert.Equal("Centro", regional.MaxBy(r => r.Score)!.Neighborhood); Assert.True(regional.Single(r => r.Neighborhood == "Centro").Score >= 75);
        var live = RiskCalculator.Calculate(new(At.AddMinutes(7), plan.Incidents, plan.Alerts, plan.Equipments, plan.Agents), new());
        Assert.Equal(regional.Single(r => r.Neighborhood == "Centro").Score, live.Single(r => r.Neighborhood == "Centro").Score);
    }
    [Fact]
    public async Task Validacao_nao_grava_e_repeticao_preserva_todos_os_registros()
    {
        await using var db = Db();
        var preview = await DemoDataLoader.ValidateAsync(db, At, "Demo", true, profile: DemoDataProfile.Evaluation);
        Assert.Equal(79, preview.TotalInserted); Assert.Empty(await db.Equipments.ToListAsync());
        Assert.Equal(79, (await Load(db)).TotalInserted);
        var before = JsonSerializer.Serialize(await db.TelemetryLogs.AsNoTracking().OrderBy(t => t.Id).Select(t => new { t.Id, t.Timestamp, t.PayloadData }).ToListAsync());
        Assert.Equal(0, (await Load(db, At.AddDays(10))).TotalInserted);
        Assert.Equal(before, JsonSerializer.Serialize(await db.TelemetryLogs.AsNoTracking().OrderBy(t => t.Id).Select(t => new { t.Id, t.Timestamp, t.PayloadData }).ToListAsync()));
        Assert.Equal(2, await db.Recommendations.CountAsync()); Assert.Equal(9, await db.MaintenanceRecords.CountAsync()); Assert.Empty(await db.Users.ToListAsync());
    }
    [Fact]
    public async Task Recusa_mistura_de_datasets_e_documentos_externos_sem_sobrescrever()
    {
        await using var db = Db(); db.Addresses.Add(new() { Street = "Registro externo", LocationReference = "OUTRO" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Load(db)); Assert.Single(await db.Addresses.ToListAsync()); Assert.Empty(await db.Equipments.ToListAsync());
    }
    [Theory]
    [InlineData("telemetry")] [InlineData("position")] [InlineData("supplier")] [InlineData("base")] [InlineData("document")]
    public async Task Repeticao_recusa_registros_externos_mesmo_em_banco_com_recursos_demo(string external)
    {
        await using var db = Db(); await Load(db);
        switch (external)
        {
            case "telemetry": db.TelemetryLogs.Add(new() { EquipmentId = DemoEvaluationPlan.Id("equipment", 0), EventType = "Heartbeat", PayloadData = "{}" }); break;
            case "position": db.AgentPositionLogs.Add(new() { AgentId = DemoEvaluationPlan.Id("agent", 0) }); break;
            case "supplier": db.Suppliers.Add(new() { Name = "Externo" }); break;
            case "base": db.OperationalBases.Add(new() { Name = "Externa", AddressId = DemoEvaluationPlan.Id("address", 0) }); break;
            case "document": (await db.Agents.FirstAsync()).Cpf = "12345678900"; break;
        }
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Load(db));
        Assert.Equal(12, await db.Equipments.CountAsync()); Assert.Equal(9, await db.MaintenanceRecords.CountAsync());
    }
    [Fact]
    public async Task Provisionamento_manual_usa_emails_invalid_e_nao_redefine_senhas_na_repeticao()
    {
        await using var db = Db();
        Assert.Equal(5, await DemoAdminProvisioner.CreateEvaluationUsersAsync(db, "SomenteTeste@2026", "Demo", true));
        var hashes = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.PasswordHash).ToListAsync();
        Assert.All(await db.Users.ToListAsync(), user => { Assert.EndsWith(".invalid", user.Email); Assert.StartsWith("DEMO ", user.Name); Assert.False(Documents.IsValidCpf(user.Cpf)); });
        Assert.Equal(0, await DemoAdminProvisioner.CreateEvaluationUsersAsync(db, "OutroTeste@2026", "Demo", true));
        Assert.Equal(hashes, await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.PasswordHash).ToListAsync());
    }
    [Theory]
    [InlineData("Production", @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Avaliacao_Teste;Integrated Security=true")]
    [InlineData("Demo", "Server=forbidden.database.windows.net;Database=SENTRA_DEMO_Avaliacao_Teste;Integrated Security=true")]
    [InlineData("Demo", @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Stage73;Integrated Security=true")]
    public void Avaliacao_recusa_azure_e_destinos_fora_do_escopo_antes_da_conexao(string environment, string connection) => Assert.Throws<InvalidOperationException>(() => DemoEvaluationTargetGuard.Validate(environment, connection));

    [Theory]
    [InlineData(UserRole.Admin, 200)] [InlineData(UserRole.OperationManager, 200)] [InlineData(UserRole.CentralOperator, 403)]
    [InlineData(UserRole.MaintenanceTechnician, 403)] [InlineData(UserRole.Agent, 403)] [InlineData(null, 401)]
    public async Task Aceite_demo_respeita_roles_e_nao_duplica_ordem(UserRole? role, int expected)
    {
        await using var host = await EquipmentTestHost.StartAsync(demo: true, interactiveDemo: true);
        using (var scope = host.App.Services.CreateScope()) await Load(scope.ServiceProvider.GetRequiredService<AppDbContext>(), DateTime.UtcNow);
        using var request = host.Request(HttpMethod.Post, $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId}/accept", role);
        using var response = await host.Client.SendAsync(request); Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 200)
        {
            using var repeat = host.Request(HttpMethod.Post, request.RequestUri!.ToString(), role); Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(repeat)).StatusCode);
            using var scope = host.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = Assert.Single(await db.MaintenanceRecords.Where(m => m.RecommendationId == DemoEvaluationPlan.PreventiveRecommendationId).ToListAsync());
            Assert.Equal(MaintenanceStatus.Scheduled, order.Status); Assert.Equal(MaintenanceKind.Preventive, order.Kind); Assert.Null(order.InitDate);
            Assert.Equal(EquipmentStatus.Operational, (await db.Equipments.SingleAsync(e => e.Id == order.EquipmentId)).Status);
            Assert.Equal(0, (await Load(db, DateTime.UtcNow.AddDays(10))).TotalInserted); Assert.Equal(10, await db.MaintenanceRecords.CountAsync());
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Leitura_demo_nao_grava_e_demais_mutacoes_continuam_bloqueadas(bool interactive)
    {
        await using var host = await EquipmentTestHost.StartAsync(demo: true, interactiveDemo: interactive);
        using (var scope = host.App.Services.CreateScope()) await Load(scope.ServiceProvider.GetRequiredService<AppDbContext>(), DateTime.UtcNow);
        foreach (var path in new[] { "/api/dashboard?includePredictive=false", "/api/dashboard/predictive", "/api/risk", "/api/equipments", "/api/agents", "/api/maintenances" })
        {
            using var request = host.Request(HttpMethod.Get, path, UserRole.Admin); Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(request)).StatusCode);
        }
        foreach (var path in new[] { "/api/predictions/analyze", "/api/incidents", $"/api/recommendations/{DemoEvaluationPlan.Id("recommendation", 1)}/accept", $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId}/dismiss" })
        {
            using var request = host.Request(HttpMethod.Post, path, UserRole.Admin); Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.SendAsync(request)).StatusCode);
        }
        if (!interactive)
        {
            using var request = host.Request(HttpMethod.Post, $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId}/accept", UserRole.Admin);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.SendAsync(request)).StatusCode);
        }
        using var final = host.App.Services.CreateScope(); var db = final.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(9, await db.MaintenanceRecords.CountAsync()); Assert.Equal(2, await db.Recommendations.CountAsync(r => r.Status == RecommendationStatus.Pending));
    }
}
