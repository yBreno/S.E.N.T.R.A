using Application.DTOs.Auth;
namespace Application.Interfaces.IServices;

public interface IPasswordRecoveryService
{
    bool Enabled { get; }
    Task RequestAsync(ForgotPasswordDto dto, string origin, CancellationToken ct);
    Task RequestAdministrativeAsync(Guid userId, AccountActor actor, string origin, CancellationToken ct);
    Task ResetAsync(ResetPasswordDto dto, string origin, CancellationToken ct);
    Task DeliverAsync(PasswordRecoveryRequest request, CancellationToken ct);
}
