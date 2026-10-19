using System.Text.Json;
using Domain.Enums;
namespace Application.DTOs.Predictive;

public record RecommendationResponseDto(Guid Id, RecommendationType Type, string Neighborhood, string Reason,
    RecommendationStatus Status, double? RiskScore, Guid? EquipmentId, DateTime CreatedAt, DateTime? ResolvedAt, JsonElement? Explanation);
