using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> b)
    {
        b.ToTable("Recommendations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Neighborhood).IsRequired().HasMaxLength(120);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        b.Property(x => x.ExplanationJson).IsRequired();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.Type, x.Neighborhood, x.Status });
        b.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
