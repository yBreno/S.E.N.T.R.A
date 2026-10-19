using Domain.Enums;
namespace Application.DTOs.Predictive;

public record CreateIncidentDto
{
    public IncidentType Type { get; init; }
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Medium;
    /// <summary>Obrigatorios quando EquipmentId nao e informado (RD-01); caso contrario herdam o endereco do equipamento.</summary>
    public string? Neighborhood { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? Description { get; init; }
    public Guid? EquipmentId { get; init; }
}

public record IncidentResponseDto(Guid Id, IncidentType Type, IncidentSeverity Severity, string Neighborhood, double Latitude,
    double Longitude, DateTime OccurredAt, string? Description, Guid? EquipmentId);

/// <summary>Resultado do fluxo integrado: evento -> status do equipamento -> alerta -> risco reavaliado -> recomendacoes.</summary>
public record IncidentRegistrationResultDto(IncidentResponseDto Incident, bool EquipmentStatusChanged,
    NeighborhoodRiskDto? RiskAfter, List<RecommendationResponseDto> NewRecommendations);
