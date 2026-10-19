using Domain.Enums;
namespace Domain.Entities;

public class Alert : BaseEntity
{
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public AlertType Type { get; set; } = AlertType.Failure;
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public DateTime? ResolvedAt { get; set; }

    public void Resolve()
    {
        if (Status == AlertStatus.Resolved) return;
        Status = AlertStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
}
