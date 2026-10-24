using System.Globalization;
using System.Text.Json;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;

// Ferramenta manual existente: sem startup da API, appsettings privados, seeders ou workers.
try
{
    var mode = args.Length == 0 ? "--plan" : args[0];
    if (!new[] { "--plan", "--validate", "--apply", "--prepare-new" }.Contains(mode)) throw new ArgumentException("Modo invalido.");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i += 2)
        if (i + 1 >= args.Length || !new[] { "--profile", "--reference-utc", "--database", "--confirm" }.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Argumentos invalidos ou duplicados.");
    var profile = options.GetValueOrDefault("--profile", "legacy") switch { "legacy" => DemoDataProfile.Legacy, "evaluation" => DemoDataProfile.Evaluation, _ => throw new ArgumentException("Perfil invalido.") };
    var at = DateTime.UtcNow;
    if (options.TryGetValue("--reference-utc", out var reference))
    {
        if (!(reference.EndsWith('Z') || reference.EndsWith("+00:00", StringComparison.Ordinal)) ||
            !DateTimeOffset.TryParse(reference, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Offset != TimeSpan.Zero) throw new ArgumentException("Referencia UTC explicita obrigatoria.");
        at = parsed.UtcDateTime;
    }
    var plan = profile == DemoDataProfile.Evaluation ? DemoEvaluationPlan.Create(at) : DemoDataPlan.Create(at);
    if (mode == "--plan")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { dataset = plan.Dataset, synthetic = true, mode = "plan-only", databaseAccessed = false, referenceUtc = at,
            addresses = plan.Addresses.Count, equipments = plan.Equipments.Count, agents = plan.Agents.Count, incidents = plan.Incidents.Count,
            alerts = plan.Alerts.Count, maintenances = plan.Maintenances.Count, telemetry = plan.Telemetry.Count, recommendations = plan.Recommendations.Count }));
        return 0;
    }
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var aspEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (environment != "Demo" || (aspEnvironment is not null && aspEnvironment != "Demo") || options.GetValueOrDefault("--confirm") != "DEMO") throw new InvalidOperationException("Ambiente e confirmacao DEMO obrigatorios.");
    var database = options.GetValueOrDefault("--database") ?? throw new ArgumentException("Destino explicito obrigatorio.");
    var connection = Environment.GetEnvironmentVariable("SENTRA_DEMO_CONNECTION_STRING") ?? "";
    DemoTargetGuard.Validate(environment, connection, database);
    if (profile == DemoDataProfile.Evaluation) DemoEvaluationTargetGuard.Validate(environment, connection, database);
    if (mode == "--prepare-new")
    {
        if (profile != DemoDataProfile.Evaluation) throw new InvalidOperationException("Preparacao nova exige perfil evaluation.");
        await DemoEvaluationSchema.PrepareNewAsync(connection, database, environment, true);
        Console.WriteLine(JsonSerializer.Serialize(new { mode, database, migrationsAppliedOnlyToNewLocalDatabase = true, dataLoaded = false })); return 0;
    }
    if (at > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("Nao carregar cenario com referencia futura.");
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    var result = mode == "--validate" ? await DemoDataLoader.ValidateAsync(db, at, environment, true, profile: profile) : await DemoDataLoader.ApplyAsync(db, at, environment, true, profile: profile);
    Console.WriteLine(JsonSerializer.Serialize(new { dataset = plan.Dataset, synthetic = true, mode, database, referenceUtc = at, result })); return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Operacao DEMO nao concluida ({ex.GetType().Name}). Confira argumentos, ambiente, destino LocalDB, schema e conflitos. Nenhuma conexao, documento ou credencial sera exibida.");
    return 1;
}
