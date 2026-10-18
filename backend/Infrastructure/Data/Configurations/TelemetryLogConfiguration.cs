using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class TelemetryLogConfiguration : IEntityTypeConfiguration<TelemetryLog>
{
    public void Configure(EntityTypeBuilder<TelemetryLog> b)
    {
        b.ToTable("TelemetryLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        b.HasIndex(x => new { x.EquipmentId, x.Timestamp });
        b.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
