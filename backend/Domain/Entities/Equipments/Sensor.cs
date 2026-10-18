using Domain.Enums;
namespace Domain.Entities;
public class Sensor() : Equipment(EquipmentType.Sensor)
{
    public string? SensorType { get; set; }
    public string? MeasurementUnit { get; set; }
}
