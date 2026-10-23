using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Tests.Support;

namespace Tests;

public class EquipmentHttpTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();

    private static object Body(string serial = "HTTP-01") => new
    {
        type = "Camera", serialNumber = serial, brand = "Marca", model = "Modelo", category = "Videomonitoramento",
        hasNightVision = true, resolution = "1080p", isColor = true,
        maintenanceNeed = "Periodic", nextMaintenanceDate = "2027-01-01T15:00:00Z",
        address = new { street = "Via sintetica", neighborhood = "Centro", city = "Sao Paulo", state = "SP", zipCode = "01001000", country = "Brasil", locationReference = "Oposto ao numero 181", latitude = -23.55, longitude = -46.63 }
    };

    [Theory]
    [InlineData(null, 401)]
    [InlineData(UserRole.Agent, 403)]
    [InlineData(UserRole.CentralOperator, 200)]
    [InlineData(UserRole.MaintenanceTechnician, 200)]
    [InlineData(UserRole.Admin, 200)]
    [InlineData(UserRole.OperationManager, 200)]
    public async Task Equipment_and_address_reads_enforce_staff_authorization(UserRole? role, int expected)
    {
        foreach (var route in new[] { "/api/equipments", "/api/addresses" })
        {
            using var request = host.Request(HttpMethod.Get, route, role);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(expected, (int)response.StatusCode);
        }
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.CentralOperator)]
    [InlineData(UserRole.MaintenanceTechnician)]
    public async Task Non_managers_cannot_create_or_edit_equipment_or_address(UserRole role)
    {
        foreach (var (method, path) in new[] { (HttpMethod.Post, "/api/equipments"), (HttpMethod.Put, $"/api/equipments/{Guid.NewGuid()}"), (HttpMethod.Post, "/api/addresses"), (HttpMethod.Put, $"/api/addresses/{Guid.NewGuid()}") })
        {
            using var request = host.Request(method, path, role);
            request.Content = JsonContent.Create(Body());
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.OperationManager)]
    public async Task Manager_can_create_read_edit_filter_and_change_status_using_existing_routes(UserRole role)
    {
        using var create = host.Request(HttpMethod.Post, "/api/equipments", role);
        create.Content = JsonContent.Create(Body());
        using var created = await host.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var equipment = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = equipment.GetProperty("id").GetString();
        var addressId = equipment.GetProperty("addressId").GetString();
        Assert.Equal("NotInstalled", equipment.GetProperty("status").GetString());
        Assert.Equal("Periodic", equipment.GetProperty("maintenanceNeed").GetString());
        using var readAddress = host.Request(HttpMethod.Get, $"/api/addresses/{addressId}", role);
        using var addressResponse = await host.Client.SendAsync(readAddress);
        var address = await addressResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Oposto ao numero 181", address.GetProperty("locationReference").GetString());
        using var update = host.Request(HttpMethod.Put, $"/api/equipments/{id}", role);
        update.Content = JsonContent.Create(new { serialNumber = "HTTP-EDITED", brand = "Nova marca", model = "Novo modelo", addressId, category = "Camera viaria", resolution = "4K", hasNightVision = true });
        using var updated = await host.Client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Periodic", (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("maintenanceNeed").GetString());
        using var patch = host.Request(HttpMethod.Patch, $"/api/equipments/{id}/status", role);
        patch.Content = JsonContent.Create(new { newStatus = "Operational", reason = "Instalacao concluida" });
        using var changed = await host.Client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var search = host.Request(HttpMethod.Get, "/api/equipments?type=Camera&status=Operational&neighborhood=Centro", role);
        using var found = await host.Client.SendAsync(search);
        var list = await found.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(list.EnumerateArray());
        using var detail = host.Request(HttpMethod.Get, $"/api/equipments/{id}", role);
        using var detailResponse = await host.Client.SendAsync(detail);
        Assert.Equal("4K", (await detailResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attributes").GetProperty("resolution").GetString());
    }

    [Fact]
    public async Task Duplicate_and_validation_errors_keep_the_existing_HTTP_contract()
    {
        using var first = host.Request(HttpMethod.Post, "/api/equipments", UserRole.Admin);
        first.Content = JsonContent.Create(Body());
        using var created = await host.Client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = host.Request(HttpMethod.Post, "/api/equipments", UserRole.Admin);
        duplicate.Content = JsonContent.Create(Body());
        using var conflict = await host.Client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var invalid = host.Request(HttpMethod.Post, "/api/equipments", UserRole.Admin);
        invalid.Content = JsonContent.Create(new { type = "Camera", serialNumber = "", brand = "", model = "", addressId = Guid.Empty });
        using var rejected = await host.Client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("SerialNumber", out _));
    }

    [Theory]
    [InlineData(UserRole.CentralOperator)]
    [InlineData(UserRole.MaintenanceTechnician)]
    public async Task Staff_can_change_status_but_cannot_reactivate_a_deactivated_equipment(UserRole role)
    {
        using var create = host.Request(HttpMethod.Post, "/api/equipments", UserRole.Admin);
        create.Content = JsonContent.Create(Body());
        using var created = await host.Client.SendAsync(create);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        using var deactivate = host.Request(HttpMethod.Patch, $"/api/equipments/{id}/status", role);
        deactivate.Content = JsonContent.Create(new { newStatus = "Deactivated" });
        using var changed = await host.Client.SendAsync(deactivate);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var reactivate = host.Request(HttpMethod.Patch, $"/api/equipments/{id}/status", role);
        reactivate.Content = JsonContent.Create(new { newStatus = "Operational" });
        using var rejected = await host.Client.SendAsync(reactivate);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }
}
