using Domain.Enums;
namespace Domain.Entities;
public class ElectricalScooter() : Equipment(EquipmentType.ElectricalScooter)
{
    public string? BatteryCapacity { get; set; }
    public int? MaxSpeed { get; set; }
}
