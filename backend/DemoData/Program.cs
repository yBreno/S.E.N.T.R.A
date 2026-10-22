using System.Text.Json;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;

// Deliberately does not bootstrap the API, read appsettings/user-secrets, or start workers/seeders.
if (args.Length == 0 || args is ["--plan"])
{
    var plan = DemoDataPlan.Create(DateTime.UtcNow);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        dataset = DemoDataPlan.Prefix, mode = "plan-only", databaseAccessed = false,
        addresses = plan.Addresses.Count, equipments = plan.Equipments.Count, agents = plan.Agents.Count,
        incidents = plan.Incidents.Count, alerts = plan.Alerts.Count, maintenances = plan.Maintenances.Count
    }));
    return 0;
}
if (args is not ["--apply", "--database", var database, "--confirm", "DEMO"])
{
    Console.Error.WriteLine("Use --plan ou --apply --database SENTRA_DEMO_<nome> --confirm DEMO.");
    return 2;
}
try
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var aspEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (environment != "Demo" || (aspEnvironment is not null && aspEnvironment != "Demo"))
        throw new InvalidOperationException("Defina DOTNET_ENVIRONMENT=Demo; ASPNETCORE_ENVIRONMENT, se definido, tambem deve ser Demo.");
    var cs = Environment.GetEnvironmentVariable("SENTRA_DEMO_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("Configure a conexao local isolada em SENTRA_DEMO_CONNECTION_STRING, fora do codigo.");
    DemoTargetGuard.Validate(environment, cs, expectedDatabase: database);
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    var result = await DemoDataLoader.ApplyAsync(db, DateTime.UtcNow, environment, confirmed: true);
    Console.WriteLine(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception ex)
{
    // No exception body, connection strings, documents, credentials or provider diagnostics in output.
    Console.Error.WriteLine($"Carga DEMO nao concluida ({ex.GetType().Name}). Verifique ambiente, LocalDB, schema existente e conflitos conforme a documentacao.");
    return 1;
}
