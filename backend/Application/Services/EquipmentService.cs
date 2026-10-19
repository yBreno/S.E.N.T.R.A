using Application.Common;
using Application.DTOs.Equipment;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using Domain.Rules;
using FluentValidation;
namespace Application.Services;

public class EquipmentService(
    IEquipmentRepository repo, IAddressRepository addresses, IAgentRepository agents,
    IAlertRepository alerts, IMaintenanceRecordRepository maintenances, IUnitOfWork uow,
    IValidator<CreateEquipmentDto> cv, IValidator<UpdateEquipmentDto> uv, IValidator<ChangeEquipmentStatusDto> sv)
    : Service<Domain.Entities.Equipment, CreateEquipmentDto, UpdateEquipmentDto, EquipmentResponseDto>(repo, uow, cv, uv), IEquipmentService
{
    protected override string EntityName => "Equipamento";
    protected override EquipmentResponseDto ToResponse(Domain.Entities.Equipment e) => e.ToResponse();

    public async Task<IReadOnlyList<EquipmentResponseDto>> SearchAsync(EquipmentSearchDto f, CancellationToken ct = default) =>
        (await repo.SearchAsync(new EquipmentFilter(f.Type, f.Status, f.Neighborhood), ct)).Select(ToResponse).ToList();

    /// <summary>RD-01 (endereco com bairro + coordenadas validas), unicidades e regras do veiculo.</summary>
    private async Task CheckReferencesAsync(EquipmentWriteDto dto, Guid? ignoreId, bool isVehicle, CancellationToken ct)
    {
        var address = await addresses.GetByIdAsync(dto.AddressId, ct) ?? throw new NotFoundException("Endereco nao encontrado.");
        if (string.IsNullOrWhiteSpace(address.Neighborhood) || !Documents.IsValidLatitude(address.Latitude) || !Documents.IsValidLongitude(address.Longitude))
            throw new BusinessRuleException("RD-01: o endereco deve ter bairro e latitude/longitude validos.");
        if (await repo.SerialNumberExistsAsync(dto.SerialNumber.Trim(), ignoreId, ct)) throw new ConflictException("Numero de serie ja cadastrado.");

        if (!isVehicle) return;
        if (string.IsNullOrWhiteSpace(dto.LicensePlate)) throw new BusinessRuleException("Placa e obrigatoria para veiculos.");
        if (await repo.LicensePlateExistsAsync(Documents.NormalizePlate(dto.LicensePlate), ignoreId, ct)) throw new ConflictException("Placa ja cadastrada.");
        if (dto.CurrentAgentId is { } agentId)
        {
            if (!await agents.ExistsAsync(agentId, ct)) throw new NotFoundException("Agente nao encontrado.");
            if (await repo.AgentHasVehicleAsync(agentId, ignoreId, ct)) throw new ConflictException("O agente ja esta com outro veiculo.");
        }
    }

    private static void Apply(Domain.Entities.Equipment e, EquipmentWriteDto d)
    {
        e.SerialNumber = d.SerialNumber.Trim();
        e.Brand = d.Brand.Trim();
        e.Model = d.Model.Trim();
        e.AddressId = d.AddressId;

        switch (e)
        {
            case Camera c: c.HasNightVision = d.HasNightVision ?? false; c.Resolution = d.Resolution; c.IsColor = d.IsColor ?? false; break;
            case Radar r: r.MaxSpeedLimit = d.MaxSpeedLimit; r.RadarType = d.RadarType; break;
            case Sensor s: s.SensorType = d.SensorType; s.MeasurementUnit = d.MeasurementUnit; break;
            case ElectricalScooter es: es.BatteryCapacity = d.BatteryCapacity; es.MaxSpeed = d.MaxSpeed; break;
            case ParkingMeter p: p.Spots = d.Spots; break;
            case Vehicle v:
                v.LicensePlate = Documents.NormalizePlate(d.LicensePlate);
                v.Chassi = d.Chassi;
                v.ManufactureYear = d.ManufactureYear;
                v.CurrentAgentId = d.CurrentAgentId;
                if (v is Motorcycle m) m.EngineCapacityCc = d.EngineCapacityCc;
                if (v is AutonomousVehicle a) a.AutonomyLevel = d.AutonomyLevel;
                break;
        }
    }

    protected override async Task<Domain.Entities.Equipment> BuildAsync(CreateEquipmentDto dto, CancellationToken ct)
    {
        Domain.Entities.Equipment e = dto.Type switch
        {
            EquipmentType.Camera => new Camera(),
            EquipmentType.Radar => new Radar(),
            EquipmentType.Sensor => new Sensor(),
            EquipmentType.Vehicle => new Vehicle(),
            EquipmentType.ElectricalScooter => new ElectricalScooter(),
            EquipmentType.ParkingMeter => new ParkingMeter(),
            EquipmentType.Motorcycle => new Motorcycle(),
            EquipmentType.AutonomousVehicle => new AutonomousVehicle(),
            _ => throw new BusinessRuleException("Tipo de equipamento invalido.")
        };
        await CheckReferencesAsync(dto, null, e is Vehicle, ct);
        Apply(e, dto); // status inicial: NotInstalled
        return e;
    }

    protected override async Task ApplyUpdateAsync(Domain.Entities.Equipment e, UpdateEquipmentDto dto, CancellationToken ct)
    {
        await CheckReferencesAsync(dto, e.Id, e is Vehicle, ct);
        Apply(e, dto);
    }

    protected override async Task EnsureCanDeleteAsync(Domain.Entities.Equipment e, CancellationToken ct)
    {
        if (await repo.HasDependentsAsync(e.Id, ct)) throw new ConflictException("Equipamento possui manutencoes/alertas/telemetria vinculados.");
    }

    public async Task<EquipmentResponseDto> ChangeStatusAsync(Guid id, ChangeEquipmentStatusDto dto, bool isManager, CancellationToken ct = default)
    {
        await sv.ValidateAndThrowAsync(dto, ct);
        var e = await LoadAsync(id, ct);
        var from = e.Status;
        var to = dto.NewStatus;

        if (EquipmentStatusRules.RequiresManager(from, to) && !isManager)
            throw new ForbiddenException("Reativar um equipamento desativado exige perfil de gestor (Admin ou OperationManager).");
        if (to == EquipmentStatus.UnderMaintenance)
            throw new BusinessRuleException("Para colocar em manutencao, abra um registro de manutencao.");
        if (from == EquipmentStatus.UnderMaintenance && to == EquipmentStatus.Operational
            && (await maintenances.GetByEquipmentAsync(id, ct)).Any(m => m.Status == MaintenanceStatus.Open))
            throw new BusinessRuleException("Existe manutencao aberta; feche-a para retornar o equipamento a operacao.");

        e.ChangeStatus(to); // RD-02 (lanca BusinessRuleException se invalida)

        if (to == EquipmentStatus.Malfunctioning) // RF-05
        {
            e.MaintenanceNeed = MaintenanceNeed.Immediate;
            await alerts.AddAsync(new Alert
            {
                EquipmentId = e.Id,
                Severity = AlertSeverity.High,
                Message = $"Equipamento {e.SerialNumber} em mau funcionamento." + (string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" {dto.Reason.Trim()}")
            }, ct);
        }

        await UnitOfWork.SaveChangesAsync(ct);
        return ToResponse(await LoadAsync(id, ct));
    }
}
