using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
namespace Tests;

public class PredictiveHttpTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();
    private async Task<Guid> SeedAsync()
    {
        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = EquipmentRiskTests.Equipamento();
        e.MaintenanceNeed = MaintenanceNeed.Periodic; e.NextMaintenanceDate = DateTime.UtcNow.AddDays(-1);
        db.Add(e); db.AddRange(EquipmentRiskTests.Falhas(e.Id, DateTime.UtcNow));
        db.TelemetryLogs.Add(new() { EquipmentId = e.Id, Timestamp = DateTime.UtcNow.AddHours(-1), EventType = "LowBattery", PayloadData = "segredo" });
        await db.SaveChangesAsync(); return e.Id;
    }
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, UserRole role = UserRole.OperationManager)
    {
        using var request = host.Request(method, path, role);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Theory]
    [InlineData(null, 401)]
    [InlineData(UserRole.Agent, 403)]
    [InlineData(UserRole.MaintenanceTechnician, 200)]
    [InlineData(UserRole.CentralOperator, 200)]
    [InlineData(UserRole.OperationManager, 200)]
    [InlineData(UserRole.Admin, 200)]
    public async Task Consulta_de_risco_preserva_autorizacao_staff(UserRole? role, int expected)
    {
        using var request = host.Request(HttpMethod.Get, "/api/predictions/equipment-risk", role);
        using var response = await host.Client.SendAsync(request); Assert.Equal(expected, (int)response.StatusCode);
    }
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.MaintenanceTechnician)]
    public async Task Analise_e_risco_regional_negam_perfis_sem_monitoramento(UserRole role)
    {
        foreach (var (method, path) in new[] { (HttpMethod.Get, "/api/risk"), (HttpMethod.Get, "/api/predictions/anomalies"), (HttpMethod.Post, "/api/predictions/analyze") })
        {
            using var request = host.Request(method, path, role);
            using var response = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
    [Fact]
    public async Task Contratos_expoem_fatores_janelas_utc_e_nunca_payload_de_telemetria()
    {
        var id = await SeedAsync();
        var equipment = await SendAsync(HttpMethod.Get, "/api/predictions/equipment-risk");
        var item = equipment.EnumerateArray().Single(); Assert.Equal(id, item.GetProperty("equipmentId").GetGuid());
        Assert.Equal("High", item.GetProperty("level").GetString()); Assert.Equal(5, item.GetProperty("factors").GetArrayLength());
        Assert.EndsWith("Z", item.GetProperty("evaluatedAt").GetString()); Assert.EndsWith("Z", item.GetProperty("windowFrom").GetString());
        Assert.DoesNotContain("segredo", equipment.GetRawText());
        var regions = await SendAsync(HttpMethod.Get, "/api/risk");
        Assert.True(regions[0].TryGetProperty("incidentWindowFrom", out _)); Assert.Equal(5, regions[0].GetProperty("factors").GetArrayLength());
        await SendAsync(HttpMethod.Get, "/api/predictions/failures"); await SendAsync(HttpMethod.Get, "/api/predictions/anomalies");
    }
    [Fact]
    public async Task Analises_repetidas_e_concorrentes_nao_duplicam_pendencias_e_aceite_e_idempotente()
    {
        var id = await SeedAsync();
        await Task.WhenAll(SendAsync(HttpMethod.Post, "/api/predictions/analyze"), SendAsync(HttpMethod.Post, "/api/predictions/analyze"));
        var recommendations = await SendAsync(HttpMethod.Get, "/api/recommendations?status=Pending");
        var rec = Assert.Single(recommendations.EnumerateArray(), r => r.GetProperty("equipmentId").ValueKind == JsonValueKind.String && r.GetProperty("equipmentId").GetGuid() == id);
        Assert.EndsWith("Z", rec.GetProperty("createdAt").GetString());
        Assert.True(rec.GetProperty("explanation").TryGetProperty("assessment", out _));
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Empty(await db.MaintenanceRecords.ToListAsync());
            Assert.Single(await db.Alerts.Where(a => a.Type == AlertType.Preventive).ToListAsync());
        }
        var recId = rec.GetProperty("id").GetGuid();
        using (var denied = host.Request(HttpMethod.Post, $"/api/recommendations/{recId}/accept", UserRole.CentralOperator))
        using (var response = await host.Client.SendAsync(denied)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{recId}/accept");
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{recId}/accept");
        await SendAsync(HttpMethod.Post, "/api/predictions/analyze");
        using var finalScope = host.App.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = Assert.Single(await finalDb.MaintenanceRecords.ToListAsync());
        Assert.Equal(id, order.EquipmentId); Assert.Equal(MaintenanceStatus.Scheduled, order.Status);
        Assert.False(await finalDb.Recommendations.AnyAsync(r => r.EquipmentId == id && r.Status == RecommendationStatus.Pending));
    }
    [Fact]
    public async Task Recomendacao_pendente_continua_unica_apos_mudar_bairro_do_equipamento()
    {
        var id = await SeedAsync(); await SendAsync(HttpMethod.Post, "/api/predictions/analyze");
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.Equipments.Include(e => e.Address).SingleAsync(e => e.Id == id); e.Address.Neighborhood = "Outro bairro";
            await db.SaveChangesAsync();
        }
        await SendAsync(HttpMethod.Post, "/api/predictions/analyze");
        using var finalScope = host.App.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await finalDb.Recommendations.Where(r => r.EquipmentId == id && r.Status == RecommendationStatus.Pending).ToListAsync());
    }
}
