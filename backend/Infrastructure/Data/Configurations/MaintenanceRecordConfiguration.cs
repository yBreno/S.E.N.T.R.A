using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("MaintenanceRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Cost).HasPrecision(18, 2);
        b.Property(x => x.Kind).HasDefaultValue(Domain.Enums.MaintenanceKind.Corrective);
        b.HasIndex(x => new { x.EquipmentId, x.Status });
        b.HasIndex(x => x.EquipmentId).IsUnique().HasFilter("[Status] IN ('Open', 'Scheduled')");
        b.HasIndex(x => x.RecommendationId).IsUnique().HasFilter("[RecommendationId] IS NOT NULL");
        b.HasOne<Recommendation>().WithMany().HasForeignKey(x => x.RecommendationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
    }
}
