using Application.DTOs.Auth;
namespace Application.Interfaces.IServices;

public interface IPasswordRecoveryQueue
{
    bool TryEnqueue(PasswordRecoveryRequest request);
    IAsyncEnumerable<PasswordRecoveryRequest> ReadAllAsync(CancellationToken ct);
}
public interface IPasswordRecoveryEmailSender
{
    Task SendAsync(string recipient, string secret, CancellationToken ct);
}
