using Domain.Enums;
using Application.DTOs.Address;
namespace Application.DTOs.Equipment;

/// <summary>Campos comuns + campos especificos de cada subtipo (so os do tipo informado sao usados).</summary>
public abstract record EquipmentWriteDto
{
    public string SerialNumber { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public Guid AddressId { get; init; }
    public string? Category { get; init; }
    /// <summary>Alternativa a AddressId: cria uma localizacao exclusiva na mesma unidade de trabalho.</summary>
    public CreateAddressDto? Address { get; init; }
    /// <summary>Omitir preserva o planejamento existente em clientes anteriores.</summary>
    public MaintenanceNeed? MaintenanceNeed { get; init; }
    public DateTime? NextMaintenanceDate { get; init; }

    // Camera
    public bool? HasNightVision { get; init; }
    public string? Resolution { get; init; }
    public bool? IsColor { get; init; }
    // Radar
    public int? MaxSpeedLimit { get; init; }
    public string? RadarType { get; init; }
    // Sensor
    public string? SensorType { get; init; }
    public string? MeasurementUnit { get; init; }
    // ElectricalScooter
    public string? BatteryCapacity { get; init; }
    public int? MaxSpeed { get; init; }
    // ParkingMeter
    public int? Spots { get; init; }
    // Vehicle / Motorcycle / AutonomousVehicle
    public string? LicensePlate { get; init; }
    public string? Chassi { get; init; }
    public int? ManufactureYear { get; init; }
    public Guid? CurrentAgentId { get; init; }
    public int? EngineCapacityCc { get; init; }
    public int? AutonomyLevel { get; init; }
}
public record CreateEquipmentDto : EquipmentWriteDto
{
    public EquipmentType Type { get; init; }
}
/// <summary>O tipo do equipamento e imutavel.</summary>
public record UpdateEquipmentDto : EquipmentWriteDto;

public record ChangeEquipmentStatusDto(EquipmentStatus NewStatus, string? Reason);

public record EquipmentSearchDto(EquipmentType? Type, EquipmentStatus? Status, string? Neighborhood);

public record EquipmentResponseDto(Guid Id, EquipmentType Type, string SerialNumber, string Brand, string Model,
    EquipmentStatus Status, MaintenanceNeed MaintenanceNeed, DateTime? NextMaintenanceDate,
    Guid AddressId, string? Neighborhood,
    double? Latitude, double? Longitude, Dictionary<string, object?> Attributes, string? Category = null, AddressResponseDto? Address = null);
