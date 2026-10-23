using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;

namespace Tests;

public class CentralOperationsHttpTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();
    private async Task<JsonElement> GetAsync(string path, UserRole role = UserRole.CentralOperator)
    {
        using var request = host.Request(HttpMethod.Get, path, role);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Theory]
    [InlineData(UserRole.Admin, 200)]
    [InlineData(UserRole.OperationManager, 200)]
    [InlineData(UserRole.CentralOperator, 200)]
    [InlineData(UserRole.MaintenanceTechnician, 403)]
    [InlineData(UserRole.Agent, 403)]
    [InlineData(null, 401)]
    public async Task Central_preserves_monitoring_policy_without_granting_technician_or_agent_access(UserRole? role, int expected)
    {
        using var request = host.Request(HttpMethod.Get, "/api/dashboard", role);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(expected, (int)response.StatusCode);
        using var alertRequest = host.Request(HttpMethod.Get, "/api/alerts?status=Open", role);
        using var alerts = await host.Client.SendAsync(alertRequest);
        Assert.Equal(role == UserRole.MaintenanceTechnician ? 200 : expected, (int)alerts.StatusCode);
    }
    [Fact]
    public async Task Indicators_keep_existing_city_definitions_and_map_contract_includes_real_details()
    {
        Guid id;
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var address = new Address { Street = "Via teste", Number = "181", Neighborhood = "Centro", City = "São Paulo", State = States.SP, ZipCode = "01001000", LocationReference = "Em frente ao teste", Latitude = 0, Longitude = 0 };
            var states = new[] { EquipmentStatus.Operational, EquipmentStatus.Operational, EquipmentStatus.Malfunctioning, EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen, EquipmentStatus.Deactivated, EquipmentStatus.NotInstalled };
            var cameras = states.Select((state, index) =>
            {
                var camera = new Camera { SerialNumber = $"CENTRAL-{index}", Brand = "Teste", Model = "Teste", Address = address, AddressId = address.Id, Category = "Videomonitoramento" };
                if (state != EquipmentStatus.NotInstalled) camera.ChangeStatus(EquipmentStatus.Operational);
                if (state != EquipmentStatus.Operational && state != EquipmentStatus.NotInstalled) camera.ChangeStatus(state);
                return camera;
            }).ToArray();
            cameras[0].MaintenanceNeed = MaintenanceNeed.Scheduled; cameras[0].NextMaintenanceDate = DateTime.UtcNow.AddDays(2);
            db.AddRange(cameras); id = cameras[0].Id;
            var availabilities = new[] { AgentAvailability.Available, AgentAvailability.Busy, AgentAvailability.Offline };
            db.AddRange(availabilities.Select((availability, index) => new Agent { Name = $"Profissional {index}", Cpf = index.ToString("D11"), BadgeNumber = $"CENTRAL-{index}", Availability = availability, Role = (AgentRole)index, Neighborhood = "Centro" }));
            db.Alerts.Add(new Alert { EquipmentId = id, Message = "Alerta critico de teste", Severity = AlertSeverity.Critical, CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) });
            await db.SaveChangesAsync();
        }
        var dashboard = await GetAsync("/api/dashboard");
        Assert.Equal(7, dashboard.GetProperty("totalEquipments").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("activeAgents").GetInt32());
        Assert.Equal(28.57, dashboard.GetProperty("availabilityRatePercent").GetDouble());
        var operational = await GetAsync("/api/dashboard?includePredictive=false");
        Assert.Equal(JsonValueKind.Null, operational.GetProperty("predictive").ValueKind);
        Assert.Equal(dashboard.GetProperty("totalEquipments").GetInt32(), operational.GetProperty("totalEquipments").GetInt32());
        Assert.Equal(JsonValueKind.Object, dashboard.GetProperty("predictive").ValueKind);
        var counts = dashboard.GetProperty("byStatus");
        Assert.Equal(2, counts.GetProperty("Operational").GetInt32());
        Assert.Equal(3, counts.GetProperty("Malfunctioning").GetInt32() + counts.GetProperty("UnderMaintenance").GetInt32() + counts.GetProperty("Stolen").GetInt32());
        var equipment = await GetAsync($"/api/equipments/{id}");
        Assert.Equal("Operational", equipment.GetProperty("status").GetString());
        Assert.Equal("Scheduled", equipment.GetProperty("maintenanceNeed").GetString());
        Assert.Equal("01001000", equipment.GetProperty("address").GetProperty("zipCode").GetString());
        Assert.Equal("Em frente ao teste", equipment.GetProperty("address").GetProperty("locationReference").GetString());
        Assert.Equal(0, equipment.GetProperty("latitude").GetDouble());
        var alerts = await GetAsync("/api/alerts?status=Open");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("Critical", alert.GetProperty("severity").GetString());
        Assert.EndsWith("Z", alert.GetProperty("createdAt").GetString());
    }
    [Fact]
    public async Task Empty_city_exposes_zero_totals_without_fictitious_alerts()
    {
        var dashboard = await GetAsync("/api/dashboard");
        Assert.Equal(0, dashboard.GetProperty("totalEquipments").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("activeAgents").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("availabilityRatePercent").GetDouble());
        Assert.Empty((await GetAsync("/api/alerts?status=Open")).EnumerateArray());
    }
}
