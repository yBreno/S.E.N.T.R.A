using System.Text.Json;
using Application.DTOs.Predictive;
using Domain.Entities;
namespace Application.Mappings;

public static class PredictiveMappings
{
    public static IncidentResponseDto ToResponse(this Incident i) => new(i.Id, i.Type, i.Severity, i.Neighborhood, i.Latitude,
        i.Longitude, i.OccurredAt, i.Description, i.EquipmentId);

    public static AgentPositionDto ToResponse(this AgentPositionLog p) => new(p.Id, p.AgentId, p.RecordedAt, p.Neighborhood, p.Latitude, p.Longitude);

    public static RecommendationResponseDto ToResponse(this Recommendation r)
    {
        JsonElement? explanation = null;
        if (!string.IsNullOrWhiteSpace(r.ExplanationJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.ExplanationJson);
                explanation = doc.RootElement.Clone();
            }
            catch (JsonException) { /* Legado inválido: mantém a recomendação visível sem inventar evidências. */ }
        }
        return new RecommendationResponseDto(r.Id, r.Type, r.Neighborhood, r.Reason, r.Status, r.RiskScore, r.EquipmentId,
            DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
            r.ResolvedAt.HasValue ? DateTime.SpecifyKind(r.ResolvedAt.Value, DateTimeKind.Utc) : null, explanation);
    }
}
