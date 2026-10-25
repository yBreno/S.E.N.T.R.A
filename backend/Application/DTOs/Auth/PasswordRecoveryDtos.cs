namespace Application.DTOs.Auth;

public record ForgotPasswordDto(string Email)
{
    public override string ToString() => nameof(ForgotPasswordDto);
}
public record ResetPasswordDto(string Token, string NewPassword, string ConfirmPassword)
{
    public override string ToString() => nameof(ResetPasswordDto);
}
public sealed class PasswordRecoveryRequest(string email, Guid? userId = null, long? authVersion = null)
{
    public string Email { get; } = email;
    public Guid? UserId { get; } = userId;
    public long? AuthVersion { get; } = authVersion;
    public override string ToString() => nameof(PasswordRecoveryRequest);
}
