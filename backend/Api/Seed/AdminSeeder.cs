using Application.Common;
using Application.Interfaces.Security;
using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
namespace Api.Seed;

/// <summary>
/// Cria o primeiro Admin (somente Development) quando nao ha usuarios.
/// A senha vem de user-secrets: SeedAdmin:Password. Sem senha, nada e criado.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var section = config.GetSection("SeedAdmin");
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(password)) return;

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        try
        {
            if (await users.AnyAsync()) return;
            var cpf = section["Cpf"] ?? "";
            if (!Documents.IsValidCpf(cpf)) { logger.LogWarning("SeedAdmin:Cpf invalido; seed ignorado."); return; }

            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var admin = User.Create(section["Name"] ?? "Administrador", section["Email"] ?? "admin@fogo.local", cpf, hasher.Hash(password), UserRole.Admin);
            await users.AddAsync(admin);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            logger.LogInformation("Admin inicial criado: {Email}", admin.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Seed do Admin ignorado (banco indisponivel ou sem migration aplicada).");
        }
    }
}
