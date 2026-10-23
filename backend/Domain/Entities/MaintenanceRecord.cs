using Domain.Enums;
using Domain.Exceptions;
namespace Domain.Entities;

public class MaintenanceRecord : BaseEntity
{
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public Guid? AgentId { get; set; }
    public Agent? Agent { get; set; }
    public DateTime? InitDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public MaintenanceKind Kind { get; set; } = MaintenanceKind.Corrective;
    public Guid? RecommendationId { get; set; }
    public DateTime? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public MaintenanceUrgency Urgency { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public int? PeriodicityDays { get; set; }

    public void Close(DateTime endDate, decimal? cost)
    {
        if (Status != MaintenanceStatus.Open || !InitDate.HasValue) throw new BusinessRuleException("Somente uma manutencao iniciada pode ser encerrada.");
        if (endDate < InitDate) throw new BusinessRuleException("EndDate deve ser maior ou igual a InitDate.");
        EndDate = endDate;
        if (cost.HasValue) Cost = cost;
        Status = MaintenanceStatus.Closed;
    }
}
