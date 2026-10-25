using System.Net.Mail;
namespace Application.Security;

public sealed class PasswordRecoverySettings
{
    public const string SectionName = "PasswordRecovery";
    public bool Enabled { get; set; }
    public string Transport { get; set; } = "Resend";
    public string ApiKey { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string RateLimitKey { get; set; } = string.Empty;
    // Deliberately fixed: never derived from Host, Origin, request data or frontend config.
    public const string ResetUrl = "https://sentra.uno/#redefinir-senha";
    public const int ExpirationMinutes = 15;
    public bool IsSafe(string environment) =>
        (Transport == "Resend" || Transport == "LocalTest" && (environment == "Testing" || environment == "Development")) &&
        (!Enabled || RateLimitKey.Length >= 32 && (Transport == "LocalTest" ||
            ApiKey.Length >= 20 && MailAddress.TryCreate(From, out var address) && address.Address == From &&
            !From.EndsWith("@resend.dev", StringComparison.OrdinalIgnoreCase)));
}
