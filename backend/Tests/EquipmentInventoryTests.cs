using Application.DTOs.Equipment;
using Domain.Enums;
using Domain.Exceptions;
using FluentValidation;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class EquipmentInventoryTests : CenarioDeTestesDeServico
{
    private CreateEquipmentDto Input(string serial = "SPRINT-01") => new()
    {
        Type = EquipmentType.Sensor, SerialNumber = serial, Brand = "Marca", Model = "Modelo",
        Category = "Balança de caminhão", SensorType = "Pesagem", MeasurementUnit = "kg", Address = CriarEnderecoValido(),
        MaintenanceNeed = MaintenanceNeed.Scheduled, NextMaintenanceDate = DateTime.UtcNow.AddDays(10)
    };

    [Fact]
    public async Task Creates_equipment_and_full_address_in_one_save_and_roundtrips_category_and_planning()
    {
        var input = Input();
        var equipment = await Equipamentos.CreateAsync(input);
        Banco.ChangeTracker.Clear();
        var loaded = await Equipamentos.GetByIdAsync(equipment.Id);
        Assert.Equal("Balança de caminhão", loaded.Category);
        Assert.Equal("Pesagem", loaded.Attributes["sensorType"]);
        Assert.Equal(MaintenanceNeed.Scheduled, loaded.MaintenanceNeed);
        Assert.Equal(input.NextMaintenanceDate, loaded.NextMaintenanceDate);
        var address = Assert.Single(Banco.Addresses);
        Assert.Equal(address.Id, loaded.AddressId);
        Assert.Equal("01001000", address.ZipCode);
        Assert.Equal(EquipmentStatus.NotInstalled, loaded.Status);
    }

    [Fact]
    public async Task Duplicate_serial_and_invalid_address_do_not_leave_new_addresses()
    {
        await Equipamentos.CreateAsync(Input());
        await Assert.ThrowsAsync<ConflictException>(() => Equipamentos.CreateAsync(Input()));
        await Assert.ThrowsAsync<ValidationException>(() => Equipamentos.CreateAsync(Input("OTHER") with { Address = CriarEnderecoValido() with { ZipCode = "123" } }));
        Assert.Single(Banco.Addresses);
        Assert.Single(Banco.Equipments);
    }

    [Fact]
    public async Task Updating_location_creates_a_copy_and_does_not_change_other_equipment_at_shared_address()
    {
        var first = await Equipamentos.CreateAsync(Input());
        var second = await Equipamentos.CreateAsync(Input("SECOND") with { Address = null, AddressId = first.AddressId });
        var edited = await Equipamentos.UpdateAsync(first.Id, new UpdateEquipmentDto
        {
            SerialNumber = first.SerialNumber, Brand = first.Brand, Model = first.Model,
            Address = CriarEnderecoValido() with { Street = "Outra via", LocationReference = "KM 203" },
            Category = "Controlador IoT de cancela", SensorType = "Controle de acesso", MeasurementUnit = "ciclos"
        });
        Assert.NotEqual(first.AddressId, edited.AddressId);
        Assert.Equal(second.AddressId, (await Equipamentos.GetByIdAsync(second.Id)).AddressId);
        Assert.Equal("Rua A", (await Enderecos.GetByIdAsync(second.AddressId)).Street);
        Assert.Equal("KM 203", (await Enderecos.GetByIdAsync(edited.AddressId)).LocationReference);
        // Clientes antigos, sem planejamento no corpo, preservam os valores anteriores.
        Assert.Equal(first.NextMaintenanceDate, edited.NextMaintenanceDate);
        Assert.Equal(first.MaintenanceNeed, edited.MaintenanceNeed);
        Assert.Equal(EquipmentType.Sensor, edited.Type);
    }

    [Theory]
    [InlineData(MaintenanceNeed.Scheduled)]
    [InlineData(MaintenanceNeed.Periodic)]
    public async Task Planned_maintenance_requires_date(MaintenanceNeed need)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Equipamentos.CreateAsync(Input() with { MaintenanceNeed = need, NextMaintenanceDate = null }));
        Assert.Empty(Banco.Equipments);
        Assert.Empty(Banco.Addresses);
    }

    [Fact]
    public async Task Failure_need_cannot_be_hidden_by_inventory_edit_and_omitted_planning_is_preserved()
    {
        var equipment = await Equipamentos.CreateAsync(Input());
        await Equipamentos.ChangeStatusAsync(equipment.Id, new(EquipmentStatus.Operational, null), true);
        await Equipamentos.ChangeStatusAsync(equipment.Id, new(EquipmentStatus.Malfunctioning, null), true);
        var input = new UpdateEquipmentDto { SerialNumber = equipment.SerialNumber, Brand = "Nova marca", Model = "Novo modelo", AddressId = equipment.AddressId, MaintenanceNeed = MaintenanceNeed.None };
        await Assert.ThrowsAsync<BusinessRuleException>(() => Equipamentos.UpdateAsync(equipment.Id, input));
        var saved = await Equipamentos.UpdateAsync(equipment.Id, input with { MaintenanceNeed = null });
        Assert.Equal(MaintenanceNeed.Immediate, saved.MaintenanceNeed);
        Assert.Equal(EquipmentStatus.Malfunctioning, saved.Status);
    }

    [Fact]
    public async Task Existing_address_contract_and_vehicle_attributes_remain_supported()
    {
        var address = await Enderecos.CreateAsync(CriarEnderecoValido());
        var equipment = await Equipamentos.CreateAsync(new CreateEquipmentDto
        {
            Type = EquipmentType.Vehicle, SerialNumber = "SCHOOL-01", Brand = "Marca", Model = "Van", AddressId = address.Id,
            LicensePlate = "ABC-1234", Category = "Transporte escolar"
        });
        Assert.Equal("ABC1234", equipment.Attributes["licensePlate"]);
        Assert.Equal("Transporte escolar", equipment.Category);
        Assert.Equal(address.Id, equipment.AddressId);
        await Assert.ThrowsAsync<ValidationException>(() => Equipamentos.CreateAsync(Input("BOTH") with { AddressId = address.Id }));
    }

    [Fact]
    public void Sql_model_and_migration_snapshot_agree_without_connecting_to_database()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_SCHEMA_ONLY;Integrated Security=true").Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("[Category] nvarchar(80) NULL", script);
    }
}
