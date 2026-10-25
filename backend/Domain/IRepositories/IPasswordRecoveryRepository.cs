using Domain.Entities;
namespace Domain.IRepositories;

public interface IPasswordRecoveryRepository
{
    Task<PasswordResetToken?> FindTokenAsync(string hash, CancellationToken ct);
    Task<PasswordResetToken?> ForUserAsync(Guid userId, CancellationToken ct);
    void Add(PasswordResetToken token);
    Task<bool> TakeLimitAsync(string key, int maximum, DateTime now, CancellationToken ct);
    Task PruneLimitsAsync(DateTime now, CancellationToken ct);
}
