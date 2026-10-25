namespace Domain.Entities;

public sealed class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public long AuthVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Delivered { get; set; }
    public DateTime? InvalidatedAt { get; set; }
}
