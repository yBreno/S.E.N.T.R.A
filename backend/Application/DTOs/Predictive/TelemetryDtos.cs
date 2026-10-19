namespace Application.DTOs.Predictive;

public record CreateTelemetryDto
{
    public Guid EquipmentId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? PayloadData { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime? Timestamp { get; init; }
}

public record TelemetryResponseDto(Guid Id, Guid EquipmentId, DateTime Timestamp, string EventType, string? PayloadData,
    double? Latitude, double? Longitude, bool TriggeredIncident);

public record UpdateAgentLocationDto
{
    public string Neighborhood { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public Domain.Enums.AgentAvailability? Availability { get; init; }
}

public record AgentPositionDto(Guid Id, Guid AgentId, DateTime RecordedAt, string Neighborhood, double Latitude, double Longitude);
