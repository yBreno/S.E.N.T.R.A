namespace Application.DTOs.Predictive;

public record PredictiveExecutionDto(string Status, string? Origin, DateTime? StartedAt, DateTime? FinishedAt,
    DateTime? LastCompletedAt, DateTime? LastAutomaticCompletedAt, int? RecommendationsCreated, int? PreventiveAlertsCreated,
    string Scope = "InstanceMemory");
public record PredictiveAutomationStatusDto(bool Enabled, int IntervalMinutes, bool MultipleInstancesSupported, PredictiveExecutionDto Execution);
public record PredictiveOperationsDto(DateTime EvaluatedAt, IReadOnlyList<EquipmentRiskDto> EquipmentRisks,
    IReadOnlyList<NeighborhoodRiskDto> Neighborhoods, IReadOnlyList<RecommendationResponseDto> Recommendations,
    PredictiveAutomationStatusDto Automation);
