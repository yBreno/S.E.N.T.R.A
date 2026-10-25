namespace Domain.Entities;

/// <summary>One current capability per account. The secret itself is never persisted.</summary>
public sealed class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public long AuthVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Delivered { get; set; }
    public DateTime? InvalidatedAt { get; set; }
}
