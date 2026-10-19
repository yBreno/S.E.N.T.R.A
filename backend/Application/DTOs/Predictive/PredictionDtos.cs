namespace Application.DTOs.Predictive;

public record AnomalyDto(string Kind, string Subject, string Neighborhood, Guid? EquipmentId, double Current, double Mean,
    double StdDev, double Threshold, double? ZScore, string Explanation, int RecordCount, List<Guid> RecordIds, DateTime DetectedAt);

public record FailurePredictionDto(Guid EquipmentId, string SerialNumber, string EquipmentType, string Neighborhood,
    double MtbfDays, string MtbfBasis, int SampleSize, double DaysSinceLastRestore, double Ratio, double EstimatedDaysToFailure,
    string State, string Explanation, List<Guid> FailureAlertIds);

public record PredictiveAnalysisResultDto(int PreventiveAlertsCreated, int RecommendationsCreated,
    List<AnomalyDto> Anomalies, List<FailurePredictionDto> EquipmentAtRisk);

public record PredictiveSummaryDto(List<RiskSummaryDto> TopRiskNeighborhoods, int PendingRecommendations, int ActiveAnomalies, int EquipmentsAtRisk);
