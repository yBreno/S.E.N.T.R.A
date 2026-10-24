namespace Application.Interfaces.IServices;

public interface IPredictiveWriteCoordinator
{
    bool SupportsMultipleInstances { get; }
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
