namespace Domain.Entities;

public sealed class PasswordRecoveryLimit
{
    public string Key { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Count { get; set; }
}
