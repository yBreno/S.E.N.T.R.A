using Domain.Enums;
using Domain.Exceptions;
namespace Domain.Entities;

public class Recommendation : BaseEntity
{
    public RecommendationType Type { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Pending;
    public double? RiskScore { get; set; }
    /// <summary>JSON com fatores, regra acionada e registros usados (explicabilidade).</summary>
    public string ExplanationJson { get; set; } = "{}";
    public Guid? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public void Accept() { EnsurePending(); Status = RecommendationStatus.Accepted; ResolvedAt = DateTime.UtcNow; }
    public void Dismiss() { EnsurePending(); Status = RecommendationStatus.Dismissed; ResolvedAt = DateTime.UtcNow; }

    private void EnsurePending()
    {
        if (Status != RecommendationStatus.Pending) throw new BusinessRuleException($"A recomendacao ja foi tratada ({Status}).");
    }
}
