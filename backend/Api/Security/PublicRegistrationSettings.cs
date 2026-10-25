namespace Api.Security;

public sealed class PublicRegistrationSettings
{
    public bool AllowAgentSelfRegistration { get; set; } = false;
    public static bool IsSafe(string environment, bool allow) => !allow ||
        string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase);
}
