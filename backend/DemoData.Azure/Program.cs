
using System.Text.Json;
using DemoData.Azure;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;

const string usage = "Use --plan ou --apply --environment AzureSqlDemo --server <servidor.database.windows.net> --database <banco> --confirm PLAN|APPLY-SENTRA-DEMO-v1.";

var etapa = "Validando argumentos";

try
{
    if (args.Length == 0)
    {
        var offlinePlan = DemoDataPlan.Create(DateTime.UtcNow);

        Output(new
        {
            dataset = DemoDataPlan.Prefix,
            mode = "plan-only",
            databaseAccessed = false,
            addresses = offlinePlan.Addresses.Count,
            equipments = offlinePlan.Equipments.Count,
            agents = offlinePlan.Agents.Count,
            incidents = offlinePlan.Incidents.Count,
            alerts = offlinePlan.Alerts.Count,
            maintenances = offlinePlan.Maintenances.Count
        });

        return;
    }

    if (!TryRead(
            args,
            out var mode,
            out var environment,
            out var server,
            out var database,
            out var confirmation) ||
        (mode is not "--plan" and not "--apply") ||
        (mode == "--plan" && confirmation != "PLAN") ||
        (mode == "--apply" && confirmation != "APPLY-SENTRA-DEMO-v1"))
    {
        throw new InvalidOperationException(usage);
    }

    etapa = "Verificando variavel de ambiente";

    var connectionString = Environment.GetEnvironmentVariable(
        "SENTRA_AZURE_SQL_CONNECTION_STRING"
    );

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string nao configurada."
        );
    }

    etapa = "Validando destino e connection string";

    AzureDemoTargetGuard.Validate(
        environment,
        connectionString,
        server,
        database
    );

    etapa = "Preparando contexto SQL Server";

    await using var db = new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options
    );

    etapa = "Testando conexao com Azure SQL";

    try
{
    await db.Database.OpenConnectionAsync();
    await db.Database.CloseConnectionAsync();
}
catch (Microsoft.Data.SqlClient.SqlException ex)
{
    Console.Error.WriteLine(
        $"Erro SQL Azure: {ex.Number}"
    );

    throw;
}

    etapa = "Verificando migrations";

    if ((await db.Database.GetPendingMigrationsAsync()).Any())
    {
        throw new InvalidOperationException(
            "Existem migrations pendentes."
        );
    }

    etapa = "Validando registros e conflitos DEMO";

    var preview = await DemoDataLoader.ValidateAsync(
        db,
        DateTime.UtcNow,
        "Demo",
        confirmed: true,
        targetValidator: _ => AzureDemoTargetGuard.Validate(
            environment,
            connectionString,
            server,
            database
        )
    );

    object? applied = null;

    if (mode == "--apply")
    {
        etapa = "Aplicando carga DEMO";

        applied = await DemoDataLoader.ApplyAsync(
            db,
            DateTime.UtcNow,
            "Demo",
            confirmed: true,
            targetValidator: _ => AzureDemoTargetGuard.Validate(
                environment,
                connectionString,
                server,
                database
            )
        );
    }

    etapa = "Gerando relatorio";

    Output(new
    {
        dataset = DemoDataPlan.Prefix,
        mode = mode[2..],
        database,
        migrations = db.Database.GetAppliedMigrations().ToArray(),
        planned = preview,
        inserted = applied,
        writesEnabled = mode == "--apply"
    });
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Falha na etapa: {etapa}. Tipo: {ex.GetType().Name}."
    );

    Environment.ExitCode = 1;
}

static bool TryRead(
    string[] args,
    out string mode,
    out string environment,
    out string server,
    out string database,
    out string confirmation)
{
    mode = args.ElementAtOrDefault(0) ?? "";
    environment = Value(args, "--environment");
    server = Value(args, "--server");
    database = Value(args, "--database");
    confirmation = Value(args, "--confirm");

    return !string.IsNullOrWhiteSpace(environment) &&
           !string.IsNullOrWhiteSpace(server) &&
           !string.IsNullOrWhiteSpace(database) &&
           !string.IsNullOrWhiteSpace(confirmation);
}

static string Value(string[] args, string name)
{
    var index = Array.IndexOf(args, name);

    return index >= 0 && index + 1 < args.Length
        ? args[index + 1]
        : "";
}

static void Output(object value)
{
    Console.WriteLine(JsonSerializer.Serialize(value));
}
