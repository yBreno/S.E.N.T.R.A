using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
using Application.Interfaces.IServices;
using Application.DTOs.Predictive;

namespace Tests;

public class MaintenanceHttpTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();
    private async Task<Guid> EquipmentAsync(bool malfunction = false)
    {
        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var address = new Address { Street = "Via teste", Neighborhood = "Centro", City = "São Paulo", State = States.SP, ZipCode = "01001000" };
        var camera = new Camera { SerialNumber = Guid.NewGuid().ToString(), Brand = "Teste", Model = "Teste", Address = address, AddressId = address.Id };
        camera.ChangeStatus(EquipmentStatus.Operational);
        if (malfunction) camera.ChangeStatus(EquipmentStatus.Malfunctioning);
        db.Add(camera); await db.SaveChangesAsync(); return camera.Id;
    }
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body = null, HttpStatusCode expected = HttpStatusCode.OK, UserRole? role = UserRole.OperationManager)
    {
        using var request = host.Request(method, path, role);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static object Plan(Guid id) => new { equipmentId = id, description = "Inspeção preventiva", scheduledDate = DateTime.UtcNow.AddDays(1), kind = "Preventive", urgency = "Medium", periodicityDays = 30 };

    [Theory]
    [InlineData(null, 401)]
    [InlineData(UserRole.Agent, 403)]
    [InlineData(UserRole.CentralOperator, 403)]
    [InlineData(UserRole.MaintenanceTechnician, 200)]
    [InlineData(UserRole.Admin, 200)]
    [InlineData(UserRole.OperationManager, 200)]
    public async Task Maintenance_routes_preserve_role_policy(UserRole? role, int expected)
    {
        var id = Guid.NewGuid();
        foreach (var (method, route) in new[] { (HttpMethod.Get, "/api/maintenances"), (HttpMethod.Post, "/api/maintenances"), (HttpMethod.Post, "/api/maintenances/schedule"), (HttpMethod.Post, $"/api/maintenances/{id}/start"), (HttpMethod.Post, $"/api/maintenances/{id}/close"), (HttpMethod.Get, $"/api/maintenances/{id}") })
        {
            if (expected == 200 && method != HttpMethod.Get) continue;
            using var request = host.Request(method, route, role);
            if (method == HttpMethod.Post) request.Content = JsonContent.Create(Plan(id));
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(route.EndsWith(id.ToString()) && expected == 200 ? 404 : expected, (int)response.StatusCode);
        }
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.OperationManager)]
    [InlineData(UserRole.MaintenanceTechnician)]
    public async Task Preventive_lifecycle_preserves_operation_until_start_and_history_after_close(UserRole role)
    {
        var equipmentId = await EquipmentAsync();
        var order = await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(equipmentId), HttpStatusCode.Created, role);
        var id = order.GetProperty("id").GetGuid();
        Assert.Equal("Scheduled", order.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, order.GetProperty("initDate").ValueKind);
        var equipment = await SendAsync(HttpMethod.Get, $"/api/equipments/{equipmentId}", role: role);
        Assert.Equal("Operational", equipment.GetProperty("status").GetString());
        Assert.Equal("Scheduled", equipment.GetProperty("maintenanceNeed").GetString());
        await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(equipmentId), HttpStatusCode.Conflict, role);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { }, HttpStatusCode.BadRequest, role);
        await SendAsync(HttpMethod.Patch, $"/api/equipments/{equipmentId}/status", new { newStatus = "UnderMaintenance" }, HttpStatusCode.BadRequest, role);
        var started = await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/start", new { }, role: role);
        Assert.Equal("Open", started.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, started.GetProperty("initDate").ValueKind);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/start", new { }, HttpStatusCode.BadRequest, role);
        await SendAsync(HttpMethod.Patch, $"/api/equipments/{equipmentId}/status", new { newStatus = "Deactivated" }, HttpStatusCode.BadRequest, role);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { cost = -1 }, HttpStatusCode.BadRequest, role);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { endDate = DateTime.UtcNow.AddDays(-2) }, HttpStatusCode.BadRequest, role);
        var closed = await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { cost = 120.50m }, role: role);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        equipment = await SendAsync(HttpMethod.Get, $"/api/equipments/{equipmentId}", role: role);
        Assert.Equal("Operational", equipment.GetProperty("status").GetString());
        Assert.Equal("Periodic", equipment.GetProperty("maintenanceNeed").GetString());
        Assert.Equal(30, (equipment.GetProperty("nextMaintenanceDate").GetDateTime() - closed.GetProperty("endDate").GetDateTime()).TotalDays);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { }, HttpStatusCode.BadRequest, role);
        var history = await SendAsync(HttpMethod.Get, $"/api/maintenances?equipmentId={equipmentId}&status=Closed", role: role);
        Assert.Single(history.EnumerateArray());
        var empty = await SendAsync(HttpMethod.Get, "/api/maintenances?status=Scheduled", role: role);
        Assert.Empty(empty.EnumerateArray());
        using var scope = host.App.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Alerts.ToListAsync());
    }

    [Fact]
    public async Task Corrective_requires_real_failure_and_resolves_its_alert()
    {
        var equipmentId = await EquipmentAsync();
        var body = new { equipmentId, description = "Reparo corretivo", urgency = "High" };
        await SendAsync(HttpMethod.Post, "/api/maintenances", body, HttpStatusCode.BadRequest);
        await SendAsync(HttpMethod.Patch, $"/api/equipments/{equipmentId}/status", new { newStatus = "Malfunctioning", reason = "Falha de teste" });
        await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(equipmentId), HttpStatusCode.BadRequest);
        var order = await SendAsync(HttpMethod.Post, "/api/maintenances", body, HttpStatusCode.Created);
        var id = order.GetProperty("id").GetGuid();
        Assert.Equal("Corrective", order.GetProperty("kind").GetString());
        await SendAsync(HttpMethod.Post, "/api/maintenances", body, HttpStatusCode.Conflict);
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { });
        using var scope = host.App.Services.CreateScope();
        var alert = Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Alerts.ToListAsync());
        Assert.Equal(AlertStatus.Resolved, alert.Status);
    }

    [Fact]
    public async Task Invalid_plans_do_not_create_orders_or_change_equipment()
    {
        var id = await EquipmentAsync();
        foreach (var body in new object[] {
            new { equipmentId = id, description = "", scheduledDate = DateTime.UtcNow.AddDays(1) },
            new { equipmentId = id, description = "Inspecao", scheduledDate = DateTime.UtcNow.AddDays(-1) },
            new { equipmentId = id, description = "Inspecao", scheduledDate = DateTime.UtcNow.AddDays(1), periodicityDays = 0 },
            new { equipmentId = id, description = "Inspecao", scheduledDate = DateTime.UtcNow.AddDays(1), kind = "Unknown" } })
            await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", body, HttpStatusCode.BadRequest);
        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.MaintenanceRecords.ToListAsync());
        Assert.Equal(EquipmentStatus.Operational, (await db.Equipments.FindAsync(id))!.Status);
    }

    [Fact]
    public async Task Scheduled_order_protects_planning_but_allows_identification_updates()
    {
        var id = await EquipmentAsync();
        await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(id), HttpStatusCode.Created);
        var equipment = await SendAsync(HttpMethod.Get, $"/api/equipments/{id}");
        var body = new { serialNumber = equipment.GetProperty("serialNumber").GetString(), brand = "Atualizada", model = "Teste", addressId = equipment.GetProperty("addressId").GetGuid(), hasNightVision = false, isColor = false, resolution = "1080p", maintenanceNeed = "None" };
        await SendAsync(HttpMethod.Put, $"/api/equipments/{id}", body, HttpStatusCode.BadRequest);
        var update = await SendAsync(HttpMethod.Put, $"/api/equipments/{id}", new { body.serialNumber, body.brand, body.model, body.addressId, body.hasNightVision, body.isColor, body.resolution });
        Assert.Equal("Atualizada", update.GetProperty("brand").GetString());
        Assert.Equal("Scheduled", update.GetProperty("maintenanceNeed").GetString());
        Assert.Equal(equipment.GetProperty("nextMaintenanceDate").GetString(), update.GetProperty("nextMaintenanceDate").GetString());
        await SendAsync(HttpMethod.Patch, $"/api/equipments/{id}/status", new { newStatus = "Deactivated" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Real_failure_after_scheduling_becomes_corrective_without_losing_order()
    {
        var equipmentId = await EquipmentAsync();
        var order = await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(equipmentId), HttpStatusCode.Created);
        var id = order.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Patch, $"/api/equipments/{equipmentId}/status", new { newStatus = "Malfunctioning", reason = "Falha apos agendamento" });
        var started = await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/start", new { });
        Assert.Equal("Corrective", started.GetProperty("kind").GetString());
        Assert.Equal(order.GetProperty("scheduledDate").GetString(), started.GetProperty("scheduledDate").GetString());
        await SendAsync(HttpMethod.Post, $"/api/maintenances/{id}/close", new { });
        Assert.Single((await SendAsync(HttpMethod.Get, $"/api/maintenances?equipmentId={equipmentId}")).EnumerateArray());
        using var scope = host.App.Services.CreateScope();
        Assert.Equal(AlertStatus.Resolved, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Alerts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Predictions_and_repeated_failure_do_not_recommend_equipment_with_pending_order()
    {
        var id = await EquipmentAsync();
        await SendAsync(HttpMethod.Post, "/api/maintenances/schedule", Plan(id), HttpStatusCode.Created);
        using var scope = host.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
        Assert.Null(await service.CreateForPredictionAsync(new FailurePredictionDto(id, "Teste", "Camera", "Centro", 30, "Teste", 2, 25, .9, 3, "Warning", "Teste", [])));
        Assert.Null(await service.CreateForAnomalyAsync(new AnomalyDto("RepeatedFailure", "Teste", "Centro", id, 5, 2, 1, 3, 3, "Teste", 2, [], DateTime.UtcNow)));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Recommendations.ToListAsync());
    }

    [Theory]
    [InlineData(UserRole.CentralOperator)]
    [InlineData(UserRole.MaintenanceTechnician)]
    [InlineData(UserRole.Agent)]
    public async Task Only_managers_may_accept_recommendations(UserRole role)
    {
        using var request = host.Request(HttpMethod.Post, $"/api/recommendations/{Guid.NewGuid()}/accept", role);
        request.Content = JsonContent.Create(new { });
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Recommendation_acceptance_creates_one_order_atomically_and_retries_are_idempotent()
    {
        var equipmentId = await EquipmentAsync();
        var recommendation = new Recommendation { Type = RecommendationType.SendMaintenance, EquipmentId = equipmentId, Reason = "Inspecao prevista", Neighborhood = "Centro" };
        var duplicate = new Recommendation { Type = RecommendationType.SendMaintenance, EquipmentId = equipmentId, Reason = "Outra previsao", Neighborhood = "Centro" };
        var neighborhood = new Recommendation { Type = RecommendationType.SendMaintenance, Reason = "Revisar bairro", Neighborhood = "Centro" };
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(recommendation, duplicate, neighborhood); await db.SaveChangesAsync();
        }
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{recommendation.Id}/accept", new { });
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{recommendation.Id}/accept", new { });
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{duplicate.Id}/accept", new { }, HttpStatusCode.Conflict);
        await SendAsync(HttpMethod.Post, $"/api/recommendations/{neighborhood.Id}/accept", new { }, HttpStatusCode.BadRequest);
        using var review = host.App.Services.CreateScope();
        var database = review.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = Assert.Single(await database.MaintenanceRecords.ToListAsync());
        Assert.Equal(recommendation.Id, record.RecommendationId);
        Assert.Equal(MaintenanceStatus.Scheduled, record.Status);
        Assert.Equal(RecommendationStatus.Pending, (await database.Recommendations.FindAsync(duplicate.Id))!.Status);
        Assert.Equal(RecommendationStatus.Pending, (await database.Recommendations.FindAsync(neighborhood.Id))!.Status);
    }
}
