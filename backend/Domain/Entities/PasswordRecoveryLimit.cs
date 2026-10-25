namespace Domain.Entities;

/// <summary>HMAC key: no email or IP is stored. Shared by all API instances.</summary>
public sealed class PasswordRecoveryLimit
{
    public string Key { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Count { get; set; }
}
