using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Cnpj).HasMaxLength(14);
        b.Property(x => x.SupportPhone).HasMaxLength(20);
        b.Property(x => x.SupportEmail).HasMaxLength(200);
        b.HasIndex(x => x.Cnpj).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
    }
}
