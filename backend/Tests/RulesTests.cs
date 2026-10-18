using Application.Common;
using Application.DTOs.Address;
using Application.DTOs.Agent;
using Application.DTOs.Auth;
using Application.DTOs.Equipment;
using Application.DTOs.Maintenance;
using Application.DTOs.Organization;
using Application.Security;
using Application.Services;
using Application.Validators;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Rules;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Tests;

/// <summary>RD-02: matriz de transicao de EquipmentStatus.</summary>
public class EquipmentStatusRulesTests
{
    [Theory]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Deactivated, EquipmentStatus.Operational)]
    public void Allowed_transitions(EquipmentStatus from, EquipmentStatus to) =>
        Assert.True(EquipmentStatusRules.CanTransition(from, to));

    [Theory]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Deactivated, EquipmentStatus.Stolen)]
    public void Blocked_transitions(EquipmentStatus from, EquipmentStatus to) =>
        Assert.False(EquipmentStatusRules.CanTransition(from, to));

    [Theory]
    [InlineData(EquipmentStatus.NotInstalled)]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Stolen)]
    public void Stolen_is_final(EquipmentStatus to) =>
        Assert.False(EquipmentStatusRules.CanTransition(EquipmentStatus.Stolen, to));
}

public class DocumentsTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("11111111111", false)]
    [InlineData("52998224726", false)]
    [InlineData("", false)]
    public void Cpf(string cpf, bool expected) => Assert.Equal(expected, Documents.IsValidCpf(cpf));

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11.222.333/0001-82", false)]
    public void Cnpj(string cnpj, bool expected) => Assert.Equal(expected, Documents.IsValidCnpj(cnpj));

    [Theory]
    [InlineData("ABC-1234", true)]
    [InlineData("ABC1D23", true)]
    [InlineData("AB12345", false)]
    public void Plate(string plate, bool expected) => Assert.Equal(expected, Documents.IsValidLicensePlate(plate));

    [Fact]
    public void Cpf_is_masked() => Assert.Equal("***.982.247-**", Documents.MaskCpf("52998224725"));
}

/// <summary>Testes dos services (repositorios reais sobre EF InMemory): RD-01, RD-02 e RF-05.</summary>
public class ServiceRulesTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private AddressService Addresses => new(new AddressRepository(_db), _db, new CreateAddressDtoValidator(), new UpdateAddressDtoValidator());
    private OrganizationService Orgs => new(new OrganizationRepository(_db), _db, new CreateOrganizationDtoValidator(), new UpdateOrganizationDtoValidator());
    private AgentService Agents => new(new AgentRepository(_db), new OrganizationRepository(_db), new UserRepository(_db), _db,
        new CreateAgentDtoValidator(), new UpdateAgentDtoValidator());
    private EquipmentService Equipments => new(new EquipmentRepository(_db), new AddressRepository(_db), new OrganizationRepository(_db),
        new AgentRepository(_db), new AlertRepository(_db), new MaintenanceRecordRepository(_db), _db,
        new CreateEquipmentDtoValidator(), new UpdateEquipmentDtoValidator(), new ChangeEquipmentStatusDtoValidator());
    private MaintenanceService Maintenances => new(new MaintenanceRecordRepository(_db), new EquipmentRepository(_db), new AgentRepository(_db),
        new AlertRepository(_db), _db, new OpenMaintenanceDtoValidator(), new CloseMaintenanceDtoValidator());
    private AuthService Auth => new(new UserRepository(_db), _db, new BCryptPasswordHasher(),
        new JwtTokenService(Options.Create(new JwtSettings { Key = new string('k', 40), Issuer = "i", Audience = "a" })),
        new RegisterDtoValidator(), new LoginDtoValidator());

    private static CreateAddressDto ValidAddress() => new()
    {
        Street = "Rua A", Neighborhood = "Centro", City = "Sao Paulo", State = States.SP, ZipCode = "01001-000", Latitude = -23.55, Longitude = -46.63
    };

    private async Task<(Guid orgId, Guid addressId)> SeedBaseAsync()
    {
        var org = await Orgs.CreateAsync(new CreateOrganizationDto { Name = "CET", Type = OrganizationType.TrafficAuthority });
        var addr = await Addresses.CreateAsync(ValidAddress());
        return (org.Id, addr.Id);
    }

    private async Task<EquipmentResponseDto> NewCameraAsync(string serial = "SN-1")
    {
        var (org, addr) = await SeedBaseAsync();
        return await Equipments.CreateAsync(new CreateEquipmentDto
        {
            Type = EquipmentType.Camera, SerialNumber = serial, Brand = "B", Model = "M", OrganizationId = org, AddressId = addr
        });
    }

    // ---------- RD-01
    [Fact]
    public async Task Address_without_neighborhood_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Addresses.CreateAsync(ValidAddress() with { Neighborhood = "" }));
    }

    [Fact]
    public async Task Address_with_invalid_coordinates_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Addresses.CreateAsync(ValidAddress() with { Latitude = 91 }));
        await Assert.ThrowsAsync<ValidationException>(() => Addresses.CreateAsync(ValidAddress() with { Longitude = -181 }));
    }

    [Fact]
    public async Task Agent_without_neighborhood_or_coordinates_is_rejected()
    {
        var org = await Orgs.CreateAsync(new CreateOrganizationDto { Name = "Guarda", Type = OrganizationType.MunicipalGuard });
        var dto = new CreateAgentDto
        {
            Name = "Ana", Cpf = "52998224725", BadgeNumber = "001", Role = AgentRole.TrafficAgent, OrganizationId = org.Id,
            Neighborhood = "", Latitude = 0, Longitude = 0
        };
        await Assert.ThrowsAsync<ValidationException>(() => Agents.CreateAsync(dto));
        var ok = await Agents.CreateAsync(dto with { Neighborhood = "Centro", Latitude = -23.5, Longitude = -46.6 });
        Assert.Equal("***.982.247-**", ok.MaskedCpf); // LGPD
    }

    [Fact]
    public async Task Equipment_requires_existing_address()
    {
        var (org, _) = await SeedBaseAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => Equipments.CreateAsync(new CreateEquipmentDto
        {
            Type = EquipmentType.Camera, SerialNumber = "X", Brand = "B", Model = "M", OrganizationId = org, AddressId = Guid.NewGuid()
        }));
    }

    [Fact]
    public async Task Duplicate_serial_number_conflicts()
    {
        var cam = await NewCameraAsync("DUP");
        await Assert.ThrowsAsync<ConflictException>(() => Equipments.CreateAsync(new CreateEquipmentDto
        {
            Type = EquipmentType.Camera, SerialNumber = "DUP", Brand = "B", Model = "M", OrganizationId = cam.OrganizationId, AddressId = cam.AddressId
        }));
    }

    // ---------- RD-02
    [Fact]
    public async Task New_equipment_starts_not_installed_and_invalid_transition_fails()
    {
        var cam = await NewCameraAsync();
        Assert.Equal(EquipmentStatus.NotInstalled, cam.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Malfunctioning, null), true));
    }

    [Fact]
    public async Task Reactivating_deactivated_requires_manager()
    {
        var cam = await NewCameraAsync();
        await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Deactivated, null), false);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), false));
        var ok = await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), true);
        Assert.Equal(EquipmentStatus.Operational, ok.Status);
    }

    [Fact]
    public async Task Stolen_is_final()
    {
        var cam = await NewCameraAsync();
        await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Stolen, null), true);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), true));
    }

    // ---------- RF-05
    [Fact]
    public async Task Malfunction_creates_alert_and_maintenance_cycle_restores_operation()
    {
        var cam = await NewCameraAsync();
        await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), true);

        // Malfunctioning -> Alert automatico
        var broken = await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Malfunctioning, "sem sinal"), true);
        Assert.Equal(EquipmentStatus.Malfunctioning, broken.Status);
        Assert.Equal(MaintenanceNeed.Immediate, broken.MaintenanceNeed);
        var alert = Assert.Single(_db.Alerts);
        Assert.Equal(AlertStatus.Open, alert.Status);

        // Abrir manutencao -> UnderMaintenance
        var m = await Maintenances.OpenAsync(new OpenMaintenanceDto { EquipmentId = cam.Id, Description = "trocar modulo", PeriodicityDays = 30 });
        Assert.Equal(EquipmentStatus.UnderMaintenance, (await Equipments.GetByIdAsync(cam.Id)).Status);

        // Nao permite outra manutencao sobreposta
        await Assert.ThrowsAsync<ConflictException>(() =>
            Maintenances.OpenAsync(new OpenMaintenanceDto { EquipmentId = cam.Id, Description = "outra" }));
        // Nao pode voltar a Operational direto com manutencao aberta
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), true));

        // EndDate < InitDate e rejeitado
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Maintenances.CloseAsync(m.Id, new CloseMaintenanceDto { EndDate = m.InitDate.AddDays(-1) }));

        // Fechar -> Operational, NextMaintenanceDate, alerta resolvido
        var end = DateTime.UtcNow.AddHours(1);
        var closed = await Maintenances.CloseAsync(m.Id, new CloseMaintenanceDto { EndDate = end, Cost = 150m });
        Assert.Equal(MaintenanceStatus.Closed, closed.Status);
        var after = await Equipments.GetByIdAsync(cam.Id);
        Assert.Equal(EquipmentStatus.Operational, after.Status);
        Assert.Equal(end.AddDays(30), after.NextMaintenanceDate);
        Assert.Equal(MaintenanceNeed.Periodic, after.MaintenanceNeed);
        Assert.Equal(AlertStatus.Resolved, _db.Alerts.Single().Status);
        Assert.NotNull(_db.Alerts.Single().ResolvedAt);
    }

    [Fact]
    public async Task Maintenance_requires_malfunctioning_equipment()
    {
        var cam = await NewCameraAsync();
        await Equipments.ChangeStatusAsync(cam.Id, new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null), true);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Maintenances.OpenAsync(new OpenMaintenanceDto { EquipmentId = cam.Id, Description = "x" }));
    }

    // ---------- Auth / LGPD
    [Fact]
    public async Task Only_admin_can_register_elevated_roles_and_hash_is_never_exposed()
    {
        var dto = new RegisterDto { Name = "Joao", Email = "Joao@Fogo.com", Cpf = "52998224725", Password = "Senha@123", Role = UserRole.OperationManager };
        await Assert.ThrowsAsync<ForbiddenException>(() => Auth.RegisterAsync(dto, callerIsAdmin: false));

        var user = await Auth.RegisterAsync(dto, callerIsAdmin: true);
        Assert.Equal("joao@fogo.com", user.Email);
        Assert.DoesNotContain(typeof(UserResponseDto).GetProperties(), p => p.Name.Contains("Password") || p.Name == "Cpf");
        Assert.NotEqual("Senha@123", _db.Users.Single().PasswordHash);

        var login = await Auth.LoginAsync(new LoginDto { Email = "joao@fogo.com", Password = "Senha@123" });
        Assert.False(string.IsNullOrEmpty(login.Token));
        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth.LoginAsync(new LoginDto { Email = "joao@fogo.com", Password = "errada" }));
    }

    [Fact]
    public async Task Weak_password_and_duplicate_email_are_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Auth.RegisterAsync(new RegisterDto { Name = "A", Email = "a@a.com", Cpf = "52998224725", Password = "fraca" }, false));
        var ok = new RegisterDto { Name = "A", Email = "a@a.com", Cpf = "52998224725", Password = "Forte@1234" };
        await Auth.RegisterAsync(ok, false);
        await Assert.ThrowsAsync<ConflictException>(() => Auth.RegisterAsync(ok with { Cpf = "11144477735" }, false));
    }
}
