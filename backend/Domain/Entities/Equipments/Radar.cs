using Domain.Enums;
namespace Domain.Entities;
public class Radar() : Equipment(EquipmentType.Radar)
{
    public int? MaxSpeedLimit { get; set; }
    public string? RadarType { get; set; }
}
