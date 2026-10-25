using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using FluentValidation;
namespace Api.Seed;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var section = config.GetSection("SeedAdmin"); var password = section["Password"];
        if (string.IsNullOrWhiteSpace(password)) return;
        using var scope = services.CreateScope();
        try
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var coordinator = scope.ServiceProvider.GetRequiredService<IUserAdministrationCoordinator>();
            var created = await coordinator.ExecuteAsync(async () =>
            {
                if (await users.AnyAsync()) return false;
                var dto = new RegisterDto { Name = section["Name"] ?? "Administrador", Email = section["Email"] ?? "admin@fogo.local", Cpf = section["Cpf"] ?? "", Password = password, Role = UserRole.Admin };
                new RegisterDtoValidator().ValidateAndThrow(dto);
                var user = User.Create(dto.Name, dto.Email, dto.Cpf, scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password), UserRole.Admin);
                await users.AddAsync(user);
                await scope.ServiceProvider.GetRequiredService<IUserAuditRepository>().AddAsync(new() { UserId = user.Id, Action = "InitialProvisioning", NewRole = UserRole.Admin });
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(); return true;
            });
            if (created) logger.LogInformation("Primeiro administrador provisionado. Nenhuma credencial será exibida.");
        }
        catch (Exception ex) { logger.LogWarning("Bootstrap não concluído. Tipo: {FailureType}. Verifique configuração privada e schema.", ex.GetType().Name); }
    }
}
