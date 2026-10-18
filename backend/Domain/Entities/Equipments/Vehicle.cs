using Domain.Enums;
namespace Domain.Entities;

public class Vehicle : Equipment
{
    public Vehicle() : base(EquipmentType.Vehicle) { }
    protected Vehicle(EquipmentType type) : base(type) { }

    public string LicensePlate { get; set; } = string.Empty;
    public string? Chassi { get; set; }
    public int? ManufactureYear { get; set; }
    public Guid? CurrentAgentId { get; set; }
    public Agent? CurrentAgent { get; set; }
}

public class Motorcycle : Vehicle
{
    public Motorcycle() : base(EquipmentType.Motorcycle) { }
    public int? EngineCapacityCc { get; set; }
}

public class AutonomousVehicle : Vehicle
{
    public AutonomousVehicle() : base(EquipmentType.AutonomousVehicle) { }
    public int? AutonomyLevel { get; set; }
}
