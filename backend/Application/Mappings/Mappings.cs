using Application.Common;
using Application.DTOs.Address;
using Application.DTOs.Agent;
using Application.DTOs.Alert;
using Application.DTOs.Auth;
using Application.DTOs.Equipment;
using Application.DTOs.Maintenance;
using Domain.Entities;
namespace Application.Mappings;

public static class Mappings
{
    public static AddressResponseDto ToResponse(this Domain.Entities.Address a) => new(a.Id, a.Street, a.Number, a.Complement,
        a.Neighborhood, a.City, a.State, a.ZipCode, a.LocationReference, a.Country, a.Latitude, a.Longitude);

    public static void Apply(this Domain.Entities.Address a, AddressWriteDto d)
    {
        a.Street = d.Street.Trim();
        a.Number = d.Number?.Trim();
        a.Complement = d.Complement?.Trim();
        a.Neighborhood = d.Neighborhood.Trim();
        a.City = d.City.Trim();
        a.State = d.State;
        a.ZipCode = Documents.Digits(d.ZipCode);
        a.LocationReference = d.LocationReference?.Trim();
        a.Country = d.Country.Trim();
        a.Latitude = d.Latitude;
        a.Longitude = d.Longitude;
    }

    public static AgentResponseDto ToResponse(this Domain.Entities.Agent a) => new(a.Id, a.Name, Documents.MaskCpf(a.Cpf),
        a.BadgeNumber, a.Role, a.Availability, a.UserId,
        a.Neighborhood, a.Latitude, a.Longitude,
        // SQL datetime2 loses Kind; persisted agent locations are written in UTC.
        a.LocationUpdatedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(a.LocationUpdatedAt, DateTimeKind.Utc)
            : a.LocationUpdatedAt.ToUniversalTime());

    public static void Apply(this Domain.Entities.Agent a, AgentWriteDto d)
    {
        a.Name = d.Name.Trim();
        a.BadgeNumber = d.BadgeNumber.Trim();
        a.Role = d.Role;
        a.UserId = d.UserId;
        a.Availability = d.Availability;
        if (a.Neighborhood != d.Neighborhood.Trim() || a.Latitude != d.Latitude || a.Longitude != d.Longitude)
            a.LocationUpdatedAt = DateTime.UtcNow;
        a.Neighborhood = d.Neighborhood.Trim();
        a.Latitude = d.Latitude;
        a.Longitude = d.Longitude;
    }

    public static UserResponseDto ToResponse(this User u) => new(u.Id, u.Name, u.Email, Documents.MaskCpf(u.Cpf), u.Role);

    public static AlertResponseDto ToResponse(this Domain.Entities.Alert a) =>
        new(a.Id, a.EquipmentId, a.Type, a.Severity, a.Message, a.Status, Utc(a.CreatedAt)!.Value, Utc(a.ResolvedAt));

    public static MaintenanceResponseDto ToResponse(this MaintenanceRecord m) => new(m.Id, m.EquipmentId, m.AgentId, Utc(m.InitDate),
        Utc(m.EndDate), m.Description, m.Cost, m.Urgency, m.Status, m.PeriodicityDays, m.Kind, Utc(m.ScheduledDate), m.RecommendationId);

    private static DateTime? Utc(DateTime? value) => value is { } date
        ? date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime() : null;

    public static EquipmentResponseDto ToResponse(this Domain.Entities.Equipment e) => new(e.Id, e.Type, e.SerialNumber, e.Brand, e.Model,
        e.Status, e.MaintenanceNeed, e.NextMaintenanceDate is { } nextMaintenance
            ? nextMaintenance.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(nextMaintenance, DateTimeKind.Utc) : nextMaintenance.ToUniversalTime()
            : null, e.AddressId,
        e.Address?.Neighborhood, e.Address?.Latitude, e.Address?.Longitude, Attributes(e), e.Category, e.Address?.ToResponse());

    private static Dictionary<string, object?> VehicleAttrs(Vehicle v) => new()
    {
        ["licensePlate"] = v.LicensePlate, ["chassi"] = v.Chassi, ["manufactureYear"] = v.ManufactureYear, ["currentAgentId"] = v.CurrentAgentId
    };

    private static Dictionary<string, object?> Attributes(Domain.Entities.Equipment e)
    {
        switch (e)
        {
            case Camera c: return new() { ["hasNightVision"] = c.HasNightVision, ["resolution"] = c.Resolution, ["isColor"] = c.IsColor };
            case Radar r: return new() { ["maxSpeedLimit"] = r.MaxSpeedLimit, ["radarType"] = r.RadarType };
            case Sensor s: return new() { ["sensorType"] = s.SensorType, ["measurementUnit"] = s.MeasurementUnit };
            case ElectricalScooter es: return new() { ["batteryCapacity"] = es.BatteryCapacity, ["maxSpeed"] = es.MaxSpeed };
            case ParkingMeter p: return new() { ["spots"] = p.Spots };
            case Motorcycle m: { var a = VehicleAttrs(m); a["engineCapacityCc"] = m.EngineCapacityCc; return a; }
            case AutonomousVehicle av: { var a = VehicleAttrs(av); a["autonomyLevel"] = av.AutonomyLevel; return a; }
            case Vehicle v: return VehicleAttrs(v);
            default: return new();
        }
    }
}
