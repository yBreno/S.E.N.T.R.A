using Application.Interfaces.IServices;
using Application.Predictive;
using Microsoft.Extensions.Options;
namespace Api.Workers;

public sealed class PredictiveAutomationWorker(IServiceScopeFactory scopes, IOptions<PredictiveAutomationSettings> options,
    TimeProvider clock, ILogger<PredictiveAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.HasValidInterval) throw new OptionsValidationException(PredictiveAutomationSettings.SectionName,
            typeof(PredictiveAutomationSettings), ["IntervalMinutes deve estar entre 15 e 1440 minutos."]);
        if (!settings.Enabled) return;
        using (var scope = scopes.CreateScope())
        {
            if (!scope.ServiceProvider.GetRequiredService<IPredictiveWriteCoordinator>().SupportsMultipleInstances)
            {
                logger.LogError("Automação preditiva bloqueada: provedor sem coordenação distribuída. Nenhuma análise foi iniciada.");
                return;
            }
        }
        try
        {
            // Primeiro ciclo após um intervalo completo; próximo intervalo só depois da conclusão.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), clock, stoppingToken);
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IPredictiveAnalysisService>().RunAutomaticAsync(stoppingToken);
                    logger.LogInformation("Análise automática concluída: {Recommendations} recomendações e {Alerts} alertas preventivos.", result.RecommendationsCreated, result.PreventiveAlertsCreated);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning("Ciclo preditivo não concluído. Tipo de falha: {FailureType}. Nova tentativa após o intervalo configurado.", ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
