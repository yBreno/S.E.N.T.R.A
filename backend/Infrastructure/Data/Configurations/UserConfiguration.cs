using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Email).IsRequired().HasMaxLength(200);
        b.Property(x => x.Cpf).IsRequired().HasMaxLength(11).IsFixedLength();
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.Cpf).IsUnique();
    }
}
