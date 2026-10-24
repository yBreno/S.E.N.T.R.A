using Domain.Enums;
namespace Application.DTOs.Predictive;

/// <summary>Índice heurístico explicável; não representa probabilidade de falha.</summary>
public record EquipmentRiskDto(Guid EquipmentId, string SerialNumber, string EquipmentType, EquipmentStatus Status,
    string Neighborhood, double Score, RiskLevel Level, DateTime EvaluatedAt, DateTime WindowFrom,
    bool HasSufficientHistory, string HistoryDescription, string Summary, string? RecommendedAction,
    List<RiskFactorDto> Factors);
