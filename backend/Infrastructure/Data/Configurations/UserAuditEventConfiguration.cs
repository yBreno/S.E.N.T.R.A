using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class UserAuditEventConfiguration : IEntityTypeConfiguration<UserAuditEvent>
{
    public void Configure(EntityTypeBuilder<UserAuditEvent> b)
    {
        b.ToTable("UserAuditEvents"); b.HasKey(e => e.Id);
        b.Property(e => e.Action).IsRequired().HasMaxLength(40);
        b.Property(e => e.PreviousRole).HasConversion<string>().HasMaxLength(40);
        b.Property(e => e.NewRole).HasConversion<string>().HasMaxLength(40);
        b.HasIndex(e => new { e.UserId, e.CreatedAt, e.Id });
        b.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
