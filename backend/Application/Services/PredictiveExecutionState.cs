using Application.DTOs.Predictive;
namespace Application.Services;

/// <summary>Memória da instância; não é histórico persistente nem estado de outras réplicas.</summary>
public sealed class PredictiveExecutionState(TimeProvider clock)
{
    private readonly object sync = new();
    private PredictiveExecutionDto state = new("NotRun", null, null, null, null, null, null, null);
    public PredictiveExecutionDto Snapshot() { lock (sync) return state; }
    public void Start(string origin)
    {
        lock (sync) state = state with { Status = "Running", Origin = origin, StartedAt = clock.GetUtcNow().UtcDateTime,
            FinishedAt = null, RecommendationsCreated = null, PreventiveAlertsCreated = null };
    }
    public void Complete(PredictiveAnalysisResultDto result)
    {
        lock (sync)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            state = state with { Status = "Completed", FinishedAt = now, LastCompletedAt = now,
                LastAutomaticCompletedAt = state.Origin == "Automatic" ? now : state.LastAutomaticCompletedAt,
                RecommendationsCreated = result.RecommendationsCreated, PreventiveAlertsCreated = result.PreventiveAlertsCreated };
        }
    }
    public void Fail() { lock (sync) state = state with { Status = "Failed", FinishedAt = clock.GetUtcNow().UtcDateTime, RecommendationsCreated = null, PreventiveAlertsCreated = null }; }
}
