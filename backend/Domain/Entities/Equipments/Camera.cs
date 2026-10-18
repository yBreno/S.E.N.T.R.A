using Domain.Enums;
namespace Domain.Entities;
public class Camera() : Equipment(EquipmentType.Camera)
{
    public bool HasNightVision { get; set; }
    public string? Resolution { get; set; }
    public bool IsColor { get; set; }
}
