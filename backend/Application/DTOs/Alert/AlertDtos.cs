using Domain.Enums;
namespace Application.DTOs.Alert;
public record AlertResponseDto(Guid Id, Guid EquipmentId, AlertSeverity Severity, string Message, AlertStatus Status,
    DateTime CreatedAt, DateTime? ResolvedAt);
