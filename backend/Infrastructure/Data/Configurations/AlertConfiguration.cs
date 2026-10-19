using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("Alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Message).IsRequired().HasMaxLength(500);
        b.Property(x => x.Type).HasDefaultValue(AlertType.Failure);
        b.HasIndex(x => new { x.EquipmentId, x.Status });
        b.HasIndex(x => new { x.Type, x.CreatedAt });
        b.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
