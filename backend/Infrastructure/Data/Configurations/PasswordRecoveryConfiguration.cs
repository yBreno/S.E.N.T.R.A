using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("PasswordResetTokens", t => t.HasCheckConstraint("CK_PasswordResetTokens_AuthVersion", "[AuthVersion] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PasswordRecoveryLimitConfiguration : IEntityTypeConfiguration<PasswordRecoveryLimit>
{
    public void Configure(EntityTypeBuilder<PasswordRecoveryLimit> b)
    {
        b.ToTable("PasswordRecoveryLimits"); b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => x.ExpiresAt);
    }
}
