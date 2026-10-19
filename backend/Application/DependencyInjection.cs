using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Application.Security;
using Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IHeatmapService, HeatmapService>();
        services.AddScoped<IAnomalyService, AnomalyService>();
        services.AddScoped<IFailurePredictionService, FailurePredictionService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<ITelemetryService, TelemetryService>();
        services.AddScoped<IAgentLocationService, AgentLocationService>();
        services.AddScoped<IPredictiveAnalysisService, PredictiveAnalysisService>();
        return services;
    }
}
