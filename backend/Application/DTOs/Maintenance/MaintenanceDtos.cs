using Domain.Enums;
namespace Application.DTOs.Maintenance;

public record OpenMaintenanceDto
{
    public Guid EquipmentId { get; init; }
    public Guid? AgentId { get; init; }
    public string Description { get; init; } = string.Empty;
    public MaintenanceUrgency Urgency { get; init; } = MaintenanceUrgency.Medium;
    public int? PeriodicityDays { get; init; }
    public DateTime? InitDate { get; init; }
}
public record CloseMaintenanceDto
{
    public DateTime? EndDate { get; init; }
    public decimal? Cost { get; init; }
}
public record MaintenanceResponseDto(Guid Id, Guid EquipmentId, Guid? AgentId, DateTime InitDate, DateTime? EndDate,
    string Description, decimal? Cost, MaintenanceUrgency Urgency, MaintenanceStatus Status, int? PeriodicityDays);
