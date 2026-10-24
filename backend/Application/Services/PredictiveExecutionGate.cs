namespace Application.Services;

/// <summary>Serializa a análise nesta instância; não é um lock distribuído.</summary>
public sealed class PredictiveExecutionGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
