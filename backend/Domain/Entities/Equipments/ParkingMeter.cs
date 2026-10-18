using Domain.Enums;
namespace Domain.Entities;
public class ParkingMeter() : Equipment(EquipmentType.ParkingMeter)
{
    public int? Spots { get; set; }
}
