using Application.Interfaces.IServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Api.Workers;

public sealed class PasswordRecoveryWorker(IPasswordRecoveryQueue queue, IServiceScopeFactory scopes, ILogger<PasswordRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().DeliverAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Recuperação assíncrona não concluída. Solicite novamente; dados privados omitidos."); }
        }
    }
}
