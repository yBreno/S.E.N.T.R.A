using Application.DTOs.Predictive;
using Domain.Enums;
namespace Application.Interfaces.IServices;

public interface IRiskService
{
    Task<IReadOnlyList<NeighborhoodRiskDto>> GetAllAsync(DateTime? at = null, CancellationToken ct = default);
    Task<NeighborhoodRiskDto?> FindByNeighborhoodAsync(string neighborhood, DateTime? at = null, CancellationToken ct = default);
    Task<NeighborhoodRiskDto> GetByNeighborhoodAsync(string neighborhood, DateTime? at = null, CancellationToken ct = default);
}

public interface IHeatmapService
{
    Task<HeatmapDto> GetAsync(HeatmapQueryDto query, CancellationToken ct = default);
}

public interface IAnomalyService
{
    Task<IReadOnlyList<AnomalyDto>> DetectAsync(CancellationToken ct = default);
}

public interface IFailurePredictionService
{
    Task<IReadOnlyList<FailurePredictionDto>> PredictAsync(bool onlyAtRisk, CancellationToken ct = default);
}

public interface IRecommendationService
{
    Task<IReadOnlyList<RecommendationResponseDto>> GetAllAsync(RecommendationStatus? status, bool maintenanceOnly, CancellationToken ct = default);
    Task<RecommendationResponseDto> GetByIdAsync(Guid id, bool maintenanceOnly, CancellationToken ct = default);
    Task<RecommendationResponseDto> AcceptAsync(Guid id, CancellationToken ct = default);
    Task<RecommendationResponseDto> DismissAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendationResponseDto>> GenerateForRiskAsync(NeighborhoodRiskDto risk, CancellationToken ct = default);
    Task<RecommendationResponseDto?> CreateForAnomalyAsync(AnomalyDto anomaly, CancellationToken ct = default);
    Task<RecommendationResponseDto?> CreateForPredictionAsync(FailurePredictionDto prediction, CancellationToken ct = default);
}

public interface IIncidentService
{
    Task<IncidentRegistrationResultDto> RegisterAsync(CreateIncidentDto dto, CancellationToken ct = default);
    Task<IncidentResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IncidentResponseDto>> SearchAsync(DateTime? from, DateTime? to, string? neighborhood, IncidentType? type, CancellationToken ct = default);
}

public interface ITelemetryService
{
    Task<TelemetryResponseDto> RegisterAsync(CreateTelemetryDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<TelemetryResponseDto>> GetByEquipmentAsync(Guid equipmentId, int take, CancellationToken ct = default);
}

public interface IAgentLocationService
{
    /// <param name="isStaff">Admin/OperationManager podem atualizar qualquer agente; o proprio agente so a si mesmo.</param>
    Task<AgentPositionDto> UpdateLocationAsync(Guid agentId, UpdateAgentLocationDto dto, Guid? callerUserId, bool isStaff, CancellationToken ct = default);
    Task<IReadOnlyList<AgentPositionDto>> GetHistoryAsync(Guid agentId, DateTime? from, DateTime? to, int take, CancellationToken ct = default);
}

public interface IPredictiveAnalysisService
{
    /// <summary>Roda anomalias + previsao MTBF + risco e gera alertas preventivos e recomendacoes (sem duplicar pendentes).</summary>
    Task<PredictiveAnalysisResultDto> RunAsync(CancellationToken ct = default);
}
