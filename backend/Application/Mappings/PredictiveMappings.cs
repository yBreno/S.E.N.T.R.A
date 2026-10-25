using System.Text.Json;
using Application.DTOs.Predictive;
using Domain.Entities;
namespace Application.Mappings;

public static class PredictiveMappings
{
    public static TelemetryResponseDto ToResponse(this TelemetryLog log, bool incidentTriggered = false) =>
        new(log.Id, log.EquipmentId, Mappings.Utc(log.Timestamp), log.EventType, log.PayloadData, log.Latitude, log.Longitude, incidentTriggered);
    public static IncidentResponseDto ToResponse(this Incident i) => new(i.Id, i.Type, i.Severity, i.Neighborhood, i.Latitude,
        i.Longitude, Mappings.Utc(i.OccurredAt), i.Description, i.EquipmentId);

    public static AgentPositionDto ToResponse(this AgentPositionLog p) => new(p.Id, p.AgentId, Mappings.Utc(p.RecordedAt), p.Neighborhood, p.Latitude, p.Longitude);

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
            Mappings.Utc(r.CreatedAt),
            r.ResolvedAt.HasValue ? Mappings.Utc(r.ResolvedAt.Value) : null, explanation);
    }
}
