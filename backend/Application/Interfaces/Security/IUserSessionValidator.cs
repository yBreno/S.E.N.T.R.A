namespace Application.Interfaces.Security;

public interface IUserSessionValidator
{
    Task<bool> IsCurrentAsync(Guid userId, string role, long authVersion, CancellationToken ct = default);
}
