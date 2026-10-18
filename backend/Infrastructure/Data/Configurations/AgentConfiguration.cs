using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("Agents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Cpf).IsRequired().HasMaxLength(11).IsFixedLength();
        b.Property(x => x.BadgeNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.Neighborhood).IsRequired().HasMaxLength(120);
        b.Property(x => x.Latitude).IsRequired();
        b.Property(x => x.Longitude).IsRequired();
        b.HasIndex(x => x.Cpf).IsUnique();
        b.HasIndex(x => x.BadgeNumber).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
