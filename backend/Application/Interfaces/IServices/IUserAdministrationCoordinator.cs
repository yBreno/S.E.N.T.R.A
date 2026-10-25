namespace Application.Interfaces.IServices;

/// <summary>Serializa alterações de contas e mantém usuário e auditoria na mesma transação.</summary>
public interface IUserAdministrationCoordinator
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
