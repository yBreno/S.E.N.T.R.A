using Api.Workers;
using Infrastructure.Demo;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Demo;

/// <summary>Pure configuration checks. Does not connect, seed, migrate or start a host.</summary>
public static class DemoApiConfiguration
{
    public const string Database = "SENTRA_DEMO_Stage73";
    public const string Url = "http://127.0.0.1:5180";
    public const string Issuer = "sentra-demo-local";
    public const string Audience = "sentra-demo-react-local";
    public static readonly string[] Origins = ["http://localhost:5173", "http://127.0.0.1:5173"];

    public static bool Prepare(ConfigurationManager configuration, IHostEnvironment environment)
    {
        var demo = environment.IsEnvironment("Demo");
        var enabled = configuration.GetValue<bool>("Demo:Enabled");
        if (!demo)
        {
            if (enabled || configuration.GetValue<bool>("Demo:InteractiveAcceptance") || !string.IsNullOrWhiteSpace(configuration["SENTRA_DEMO_DATABASE"]) || !string.IsNullOrWhiteSpace(configuration["SENTRA_DEMO_CONNECTION_STRING"]) ||
                !string.IsNullOrWhiteSpace(configuration["SENTRA_DEMO_JWT_KEY"]))
                throw new InvalidOperationException("Configuracao dedicada DEMO exige ambiente Demo.");
            return false;
        }
        if (!enabled) throw new InvalidOperationException("Ambiente Demo exige Demo:Enabled explicito.");
        if (configuration.GetValue<bool>("Simulation:Enabled", true) || configuration.GetValue<bool>("SeedAdmin:Enabled") || configuration.GetValue<bool>("PredictiveAutomation:Enabled"))
            throw new InvalidOperationException("Demo exige Simulation:Enabled e SeedAdmin:Enabled desativados.");
        var connection = configuration["SENTRA_DEMO_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure a conexao DEMO dedicada; nao ha fallback para a API existente.");
        var database = configuration["SENTRA_DEMO_DATABASE"] ?? Database;
        DemoTargetGuard.Validate("Demo", connection, database);
        if (configuration.GetValue<bool>("Demo:InteractiveAcceptance")) DemoEvaluationTargetGuard.Validate("Demo", connection, database);
        var key = configuration["SENTRA_DEMO_JWT_KEY"];
        if (string.IsNullOrWhiteSpace(key) || !ValidKey(key))
            throw new InvalidOperationException("Configure SENTRA_DEMO_JWT_KEY com pelo menos 32 bytes aleatorios em Base64.");
        if (configuration["urls"] != Url || configuration.GetSection("Kestrel:Endpoints").GetChildren().Any() ||
            !string.IsNullOrWhiteSpace(configuration["HTTP_PORTS"]) || !string.IsNullOrWhiteSpace(configuration["HTTPS_PORTS"]))
            throw new InvalidOperationException("Demo permite somente http://127.0.0.1:5180, sem endpoints Kestrel alternativos.");
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Jwt:Key"] = key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
            ["Jwt:ExpiresMinutes"] = "30", ["AllowedHosts"] = "localhost;127.0.0.1"
        });
        return true;
    }

    public static void RegisterSimulation(IServiceCollection services, bool demo)
    {
        if (!demo) services.AddHostedService<TelemetrySimulationWorker>();
    }

    public static void RegisterReadOnlyPersistence(IServiceCollection services, bool demo, bool interactiveAcceptance = false)
    {
        if (!demo) return;
        services.AddHttpContextAccessor();
        services.Configure<DemoInteractionSettings>(settings => settings.InteractiveAcceptance = interactiveAcceptance);
        services.AddDbContext<AppDbContext>((provider, options) => options.AddInterceptors(new DemoReadOnlyInterceptor(() =>
            DemoInteractionSettings.CanPersist(provider.GetRequiredService<IHttpContextAccessor>().HttpContext, interactiveAcceptance))));
    }

    public static bool RunDevelopmentSeeders(IHostEnvironment environment, bool demo) => !demo && environment.IsDevelopment();
    public static bool RunAdminSeeder(IConfiguration configuration, bool demo) => !demo && configuration.GetValue<bool>("SeedAdmin:Enabled");

    private static bool ValidKey(string key)
    {
        try { return Convert.FromBase64String(key).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
