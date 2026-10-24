using Domain.IRepositories;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<Application.Interfaces.IServices.IPredictiveWriteCoordinator, SqlPredictiveWriteCoordinator>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IMaintenanceRecordRepository, MaintenanceRecordRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IOperationalBaseRepository, OperationalBaseRepository>();
        services.AddScoped<ITelemetryLogRepository, TelemetryLogRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IAgentPositionLogRepository, AgentPositionLogRepository>();
        return services;
    }
}
