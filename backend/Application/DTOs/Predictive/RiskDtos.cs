using Domain.Enums;
namespace Application.DTOs.Predictive;

public record RiskFactorDto(string Name, string Label, double RawValue, string RawDescription, double Normalized, double Weight,
    double Points, string Rule, string RecordKind, int RecordCount, List<Guid> RecordIds);

public record NeighborhoodRiskDto(string Neighborhood, double Score, RiskLevel Level, double Latitude, double Longitude,
    DateTime EvaluatedAt, string Summary, List<RiskFactorDto> Factors,
    DateTime? IncidentWindowFrom = null, DateTime? FailureWindowFrom = null);

public record RiskSummaryDto(string Neighborhood, double Score, RiskLevel Level);
