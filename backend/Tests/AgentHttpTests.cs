using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;

namespace Tests;

public class AgentHttpTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();
    private static object Body(string cpf = "52998224725", string badge = "HTTP-AG-01", string role = "TrafficAgent") => new { name = "Agente de teste", cpf, badgeNumber = badge, role, availability = "Available", neighborhood = "Centro", latitude = -23.55, longitude = -46.63 };

    private async Task<JsonElement> Create(object? body = null, UserRole role = UserRole.Admin)
    {
        using var request = host.Request(HttpMethod.Post, "/api/agents", role);
        request.Content = JsonContent.Create(body ?? Body());
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task<HttpResponseMessage> Put(string path, object body, UserRole role, Guid? userId = null)
    {
        using var request = host.Request(HttpMethod.Put, path, role, userId);
        request.Content = JsonContent.Create(body);
        return await host.Client.SendAsync(request);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(UserRole.Agent, 403)]
    [InlineData(UserRole.MaintenanceTechnician, 403)]
    [InlineData(UserRole.CentralOperator, 200)]
    [InlineData(UserRole.OperationManager, 200)]
    [InlineData(UserRole.Admin, 200)]
    public async Task Reads_and_vehicle_details_preserve_monitoring_roles(UserRole? role, int expected)
    {
        var agent = await Create(); var id = agent.GetProperty("id").GetGuid();
        foreach (var path in new[] { "/api/agents", $"/api/agents/{id}", $"/api/agents/{id}/vehicle", $"/api/agents/{id}/positions" })
        {
            using var request = host.Request(HttpMethod.Get, path, role);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(expected, (int)response.StatusCode);
        }
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.MaintenanceTechnician)]
    [InlineData(UserRole.CentralOperator)]
    public async Task Non_managers_cannot_create_edit_or_assign_vehicles(UserRole role)
    {
        var agent = await Create(); var id = agent.GetProperty("id").GetGuid();
        using var create = host.Request(HttpMethod.Post, "/api/agents", role); create.Content = JsonContent.Create(Body("11144477735", "OTHER"));
        using var deniedCreate = await host.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
        using var deniedEdit = await Put($"/api/agents/{id}", Body(), role);
        Assert.Equal(HttpStatusCode.Forbidden, deniedEdit.StatusCode);
        using var deniedVehicle = await Put($"/api/agents/{id}/vehicle", new { vehicleId = (Guid?)null, expectedVehicleId = (Guid?)null }, role);
        Assert.Equal(HttpStatusCode.Forbidden, deniedVehicle.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.OperationManager)]
    public async Task Managers_create_edit_and_return_operational_agent_data_without_raw_CPF(UserRole role)
    {
        var agent = await Create(role: role); var id = agent.GetProperty("id").GetGuid();
        Assert.False(agent.TryGetProperty("cpf", out _));
        Assert.Equal("***.982.247-**", agent.GetProperty("maskedCpf").GetString());
        using var edited = await Put($"/api/agents/{id}", new { name = "Guarda de teste", badgeNumber = "HTTP-EDITED", role = "MunicipalGuard", availability = "Busy", neighborhood = "Mooca", latitude = -23.56, longitude = -46.60 }, role);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var saved = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MunicipalGuard", saved.GetProperty("role").GetString());
        Assert.Equal("Busy", saved.GetProperty("availability").GetString());
        Assert.EndsWith("Z", saved.GetProperty("locationUpdatedAt").GetString());
        Assert.Equal(agent.GetProperty("maskedCpf").GetString(), saved.GetProperty("maskedCpf").GetString());
    }

    [Fact]
    public async Task Invalid_and_duplicate_identity_are_HTTP_400_and_409_without_leaking_sensitive_data()
    {
        await Create();
        using var duplicate = host.Request(HttpMethod.Post, "/api/agents", UserRole.Admin); duplicate.Content = JsonContent.Create(Body());
        using var conflict = await host.Client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.DoesNotContain("52998224725", await conflict.Content.ReadAsStringAsync());
        using var invalid = host.Request(HttpMethod.Post, "/api/agents", UserRole.Admin); invalid.Content = JsonContent.Create(new { name = "", cpf = "11111111111", badgeNumber = "", role = "TrafficAgent", neighborhood = "", latitude = 91, longitude = -46.63 });
        using var rejected = await host.Client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.True((await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("Cpf", out _));
    }

    [Fact]
    public async Task Operational_location_updates_feed_dashboard_and_preserve_professional_identity()
    {
        var agent = await Create(); var id = agent.GetProperty("id").GetGuid();
        using var position = await Put($"/api/agents/{id}/location", new { neighborhood = "Pinheiros", latitude = -23.5617, longitude = -46.682, availability = "Offline" }, UserRole.CentralOperator);
        Assert.Equal(HttpStatusCode.OK, position.StatusCode);
        using var detailRequest = host.Request(HttpMethod.Get, $"/api/agents/{id}", UserRole.CentralOperator);
        using var detailResponse = await host.Client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pinheiros", detail.GetProperty("neighborhood").GetString());
        Assert.Equal("Offline", detail.GetProperty("availability").GetString());
        Assert.Equal(agent.GetProperty("name").GetString(), detail.GetProperty("name").GetString());
        using var dashboardRequest = host.Request(HttpMethod.Get, "/api/dashboard", UserRole.CentralOperator);
        using var dashboardResponse = await host.Client.SendAsync(dashboardRequest);
        Assert.Equal(0, (await dashboardResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activeAgents").GetInt32());
        using var historyRequest = host.Request(HttpMethod.Get, $"/api/agents/{id}/positions", UserRole.CentralOperator);
        using var historyResponse = await host.Client.SendAsync(historyRequest);
        Assert.Single((await historyResponse.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Agent_can_only_update_own_location_and_cannot_consult_other_profiles()
    {
        var owner = User.Create("Conta de teste", "own@sentra.invalid", "11144477735", "test-only", UserRole.Agent);
        using (var scope = host.App.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Users.Add(owner); await db.SaveChangesAsync(); }
        var agent = await Create(new { name = "Agente vinculado", cpf = "52998224725", badgeNumber = "OWN", role = "PoliceOfficer", userId = owner.Id, neighborhood = "Centro", latitude = -23.55, longitude = -46.63 });
        var id = agent.GetProperty("id").GetGuid();
        var location = new { neighborhood = "Centro", latitude = -23.56, longitude = -46.64, availability = "Available" };
        using var own = await Put($"/api/agents/{id}/location", location, UserRole.Agent, owner.Id);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var other = await Put($"/api/agents/{id}/location", location, UserRole.Agent, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using var tech = await Put($"/api/agents/{id}/location", location, UserRole.MaintenanceTechnician, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, tech.StatusCode);
        using var meRequest = host.Request(HttpMethod.Get, "/api/agents/me", UserRole.Agent, owner.Id);
        using var meResponse = await host.Client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.False((await meResponse.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("cpf", out _));
        using var restricted = host.Request(HttpMethod.Get, $"/api/agents/{id}", UserRole.Agent, owner.Id);
        using var deniedProfile = await host.Client.SendAsync(restricted);
        Assert.Equal(HttpStatusCode.Forbidden, deniedProfile.StatusCode);
    }

    [Fact]
    public async Task Vehicle_endpoint_links_replaces_and_releases_without_full_equipment_update()
    {
        var agent = await Create(); var id = agent.GetProperty("id").GetGuid();
        var vehicleIds = new List<Guid>();
        foreach (var index in new[] { 1, 2 })
        {
            using var request = host.Request(HttpMethod.Post, "/api/equipments", UserRole.Admin);
            request.Content = JsonContent.Create(new { type = "Motorcycle", serialNumber = $"MOTO-{index}", brand = "Marca", model = "Modelo", licensePlate = $"TST000{index}", engineCapacityCc = 150, address = new { street = "Via de teste", neighborhood = "Centro", city = "Sao Paulo", state = "SP", zipCode = "01001000", country = "Brasil", latitude = -23.55, longitude = -46.63 } });
            using var created = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            vehicleIds.Add((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
        using var assigned = await Put($"/api/agents/{id}/vehicle", new { vehicleId = vehicleIds[0], expectedVehicleId = (Guid?)null }, UserRole.OperationManager);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var stale = await Put($"/api/agents/{id}/vehicle", new { vehicleId = vehicleIds[1], expectedVehicleId = (Guid?)null }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var replaced = await Put($"/api/agents/{id}/vehicle", new { vehicleId = vehicleIds[1], expectedVehicleId = vehicleIds[0] }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        using var released = await Put($"/api/agents/{id}/vehicle", new { vehicleId = (Guid?)null, expectedVehicleId = vehicleIds[1] }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await released.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("vehicle").ValueKind);
    }
}
