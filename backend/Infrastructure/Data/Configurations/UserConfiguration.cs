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
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.AuthVersion).HasDefaultValue(1L);
        b.Property(x => x.Revision).HasDefaultValue(1L).IsConcurrencyToken();
        b.ToTable(t => { t.HasCheckConstraint("CK_Users_AuthVersion", "[AuthVersion] > 0"); t.HasCheckConstraint("CK_Users_Revision", "[Revision] > 0"); });
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.Cpf).IsUnique();
    }
}
