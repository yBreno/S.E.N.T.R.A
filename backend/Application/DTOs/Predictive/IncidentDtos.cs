using Domain.Enums;
namespace Application.DTOs.Predictive;

public record CreateIncidentDto
{
    public IncidentType Type { get; init; }
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Medium;
    public string? Neighborhood { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? Description { get; init; }
    public Guid? EquipmentId { get; init; }
}

public record IncidentResponseDto(Guid Id, IncidentType Type, IncidentSeverity Severity, string Neighborhood, double Latitude,
    double Longitude, DateTime OccurredAt, string? Description, Guid? EquipmentId);

public record IncidentRegistrationResultDto(IncidentResponseDto Incident, bool EquipmentStatusChanged,
    NeighborhoodRiskDto? RiskAfter, List<RecommendationResponseDto> NewRecommendations);
