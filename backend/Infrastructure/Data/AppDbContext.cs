using Domain.Entities;
using Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<OperationalBase> OperationalBases => Set<OperationalBase>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<AgentPositionLog> AgentPositionLogs => Set<AgentPositionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        // Enums legiveis no banco (string) em vez de inteiros.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
}
