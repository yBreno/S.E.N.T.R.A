using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.ToTable("Addresses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Street).IsRequired().HasMaxLength(200);
        b.Property(x => x.Number).HasMaxLength(20);
        b.Property(x => x.Complement).HasMaxLength(100);
        b.Property(x => x.Neighborhood).IsRequired().HasMaxLength(120);
        b.Property(x => x.City).IsRequired().HasMaxLength(120);
        b.Property(x => x.ZipCode).IsRequired().HasMaxLength(8);
        b.Property(x => x.LocationReference).HasMaxLength(200);
        b.Property(x => x.Country).IsRequired().HasMaxLength(60);
        b.Property(x => x.Latitude).IsRequired();
        b.Property(x => x.Longitude).IsRequired();
        b.HasIndex(x => x.Neighborhood);
    }
}
