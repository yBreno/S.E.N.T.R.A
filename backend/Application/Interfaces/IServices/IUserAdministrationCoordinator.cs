namespace Application.Interfaces.IServices;

public interface IUserAdministrationCoordinator
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
