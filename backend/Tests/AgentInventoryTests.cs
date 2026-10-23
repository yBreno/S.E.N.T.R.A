using System.Text.Json;
using Application.DTOs.Agent;
using Application.DTOs.Equipment;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentValidation;
using Infrastructure.Repositories;

namespace Tests;

public class AgentInventoryTests : CenarioDeTestesDeServico
{
    private static CreateAgentDto Agent(string cpf = "52998224725", string badge = "SPRINT-AG-01") => new()
    {
        Name = "Profissional sintetico", Cpf = cpf, BadgeNumber = badge, Role = AgentRole.TrafficAgent,
        Availability = AgentAvailability.Available, Neighborhood = "Centro", Latitude = -23.55, Longitude = -46.63
    };
    private AgentVehicleService Vehicles => new(new AgentRepository(Banco), new EquipmentRepository(Banco), Banco);
    private async Task<EquipmentResponseDto> Vehicle(string serial, string plate, EquipmentType type = EquipmentType.Vehicle)
    {
        var address = await Enderecos.CreateAsync(CriarEnderecoValido());
        return await Equipamentos.CreateAsync(new CreateEquipmentDto { Type = type, SerialNumber = serial, Brand = "Marca", Model = "Modelo", LicensePlate = plate, AddressId = address.Id, ManufactureYear = 2024 });
    }

    [Theory]
    [InlineData(AgentRole.TrafficAgent)]
    [InlineData(AgentRole.MunicipalGuard)]
    [InlineData(AgentRole.PoliceOfficer)]
    public async Task All_professional_categories_roundtrip_without_exposing_CPF(AgentRole role)
    {
        var input = Agent() with { Role = role };
        var agent = await Agentes.CreateAsync(input);
        Banco.ChangeTracker.Clear();
        var detail = await Agentes.GetByIdAsync(agent.Id);
        Assert.Equal(role, detail.Role);
        Assert.Equal("***.982.247-**", detail.MaskedCpf);
        Assert.DoesNotContain(input.Cpf, JsonSerializer.Serialize(detail));
        Assert.Equal(1, await new AgentRepository(Banco).CountActiveAsync());
    }

    [Fact]
    public async Task Duplicate_document_and_badge_are_rejected_without_creating_extra_agents()
    {
        await Agentes.CreateAsync(Agent());
        await Assert.ThrowsAsync<ConflictException>(() => Agentes.CreateAsync(Agent(badge: "OTHER")));
        await Assert.ThrowsAsync<ConflictException>(() => Agentes.CreateAsync(Agent("11144477735")));
        Assert.Single(Banco.Agents);
    }

    [Fact]
    public async Task Invalid_identity_category_coordinates_and_document_are_rejected()
    {
        foreach (var input in new[] { Agent() with { Cpf = "11111111111" }, Agent() with { Name = "" }, Agent() with { BadgeNumber = "" }, Agent() with { Role = (AgentRole)99 }, Agent() with { Availability = (AgentAvailability)99 }, Agent() with { Latitude = 91 }, Agent() with { Neighborhood = "" } })
            await Assert.ThrowsAsync<ValidationException>(() => Agentes.CreateAsync(input));
        Assert.Empty(Banco.Agents);
    }

    [Fact]
    public async Task Edit_changes_location_and_activity_while_preserving_CPF_and_access_account()
    {
        var user = User.Create("Conta sintetica", "agent@sentra.invalid", "11144477735", "test-only", UserRole.Agent);
        Banco.Users.Add(user); await Banco.SaveChangesAsync();
        var created = await Agentes.CreateAsync(Agent() with { UserId = user.Id });
        var saved = await Agentes.UpdateAsync(created.Id, new UpdateAgentDto { Name = "Nome editado", BadgeNumber = created.BadgeNumber, Role = AgentRole.MunicipalGuard, UserId = user.Id, Neighborhood = "Mooca", Latitude = -23.56, Longitude = -46.60, Availability = AgentAvailability.Offline });
        Assert.Equal(created.MaskedCpf, saved.MaskedCpf);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(0, await new AgentRepository(Banco).CountActiveAsync());
        Assert.True(saved.LocationUpdatedAt >= created.LocationUpdatedAt);
        Assert.Equal(-23.56, saved.Latitude);
    }

    [Theory]
    [InlineData(EquipmentType.Vehicle)]
    [InlineData(EquipmentType.Motorcycle)]
    [InlineData(EquipmentType.AutonomousVehicle)]
    public async Task Mobile_equipment_can_be_assigned_and_released_without_overwriting_vehicle_data(EquipmentType type)
    {
        var agent = await Agentes.CreateAsync(Agent());
        var vehicle = await Vehicle("LINK-01", "ABC1234", type);
        var linked = await Vehicles.AssignAsync(agent.Id, new(vehicle.Id, null));
        Assert.Equal(vehicle.Id, linked.Vehicle!.Id);
        Assert.Equal(agent.Id, linked.Vehicle.Attributes["currentAgentId"]);
        Assert.Equal("ABC1234", linked.Vehicle.Attributes["licensePlate"]);
        Assert.Equal(2024, linked.Vehicle.Attributes["manufactureYear"]);
        Assert.Equal(vehicle.Status, linked.Vehicle.Status);
        Assert.Equal(vehicle.Id, (await Vehicles.GetAsync(agent.Id)).Vehicle!.Id);
        Assert.Null((await Vehicles.AssignAsync(agent.Id, new(null, vehicle.Id))).Vehicle);
        Assert.Null((await Equipamentos.GetByIdAsync(vehicle.Id)).Attributes["currentAgentId"]);
    }

    [Fact]
    public async Task Replacing_a_vehicle_releases_only_the_old_link_and_rejects_stale_requests()
    {
        var agent = await Agentes.CreateAsync(Agent());
        var first = await Vehicle("FIRST", "ABC1234"); var second = await Vehicle("SECOND", "DEF5678");
        await Vehicles.AssignAsync(agent.Id, new(first.Id, null));
        await Assert.ThrowsAsync<ConflictException>(() => Vehicles.AssignAsync(agent.Id, new(second.Id, null)));
        Assert.Equal(first.Id, (await Vehicles.GetAsync(agent.Id)).Vehicle!.Id);
        await Vehicles.AssignAsync(agent.Id, new(second.Id, first.Id));
        Assert.Null((await Equipamentos.GetByIdAsync(first.Id)).Attributes["currentAgentId"]);
        Assert.Equal(agent.Id, (await Equipamentos.GetByIdAsync(second.Id)).Attributes["currentAgentId"]);
    }

    [Fact]
    public async Task Occupied_or_fixed_equipment_cannot_replace_a_valid_link()
    {
        var firstAgent = await Agentes.CreateAsync(Agent()); var other = await Agentes.CreateAsync(Agent("11144477735", "OTHER"));
        var first = await Vehicle("FIRST", "ABC1234"); var occupied = await Vehicle("OCCUPIED", "DEF5678"); var camera = await CriarCameraValidaAsync();
        await Vehicles.AssignAsync(firstAgent.Id, new(first.Id, null)); await Vehicles.AssignAsync(other.Id, new(occupied.Id, null));
        await Assert.ThrowsAsync<ConflictException>(() => Vehicles.AssignAsync(firstAgent.Id, new(occupied.Id, first.Id)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Vehicles.AssignAsync(firstAgent.Id, new(camera.Id, first.Id)));
        Assert.Equal(first.Id, (await Vehicles.GetAsync(firstAgent.Id)).Vehicle!.Id);
        Assert.Equal(occupied.Id, (await Vehicles.GetAsync(other.Id)).Vehicle!.Id);
    }
}
