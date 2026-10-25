using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Infrastructure.Data;

/// <summary>Geração offline de migrations/SQL, sem startup da API ou leitura de segredos.</summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DesignTime_Only;Integrated Security=true;TrustServerCertificate=true").Options);
}
