using Application.Interfaces.IServices;
namespace Application.Services;

/// <summary>Fallback para provedores de teste; não autoriza automação distribuída.</summary>
public sealed class InProcessPredictiveWriteCoordinator : IPredictiveWriteCoordinator, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AsyncLocal<int> depth = new();
    public bool SupportsMultipleInstances => false;
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (depth.Value > 0) return await action();
        await gate.WaitAsync(ct);
        depth.Value++;
        try { return await action(); }
        finally { depth.Value--; gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
