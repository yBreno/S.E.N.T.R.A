using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Application.Security;
using Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AuthService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
