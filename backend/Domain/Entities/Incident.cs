using Domain.Enums;
namespace Domain.Entities;

public class Incident : BaseEntity
{
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
    public string Neighborhood { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }

    public Guid? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
}
