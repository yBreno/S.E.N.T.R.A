using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.ToTable("Incidents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Neighborhood).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Latitude).IsRequired();
        b.Property(x => x.Longitude).IsRequired();
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.Neighborhood, x.OccurredAt });
        b.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
