using System.Text;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;

// Manual provisioning only. No API startup, seeders, migrations, or JWT generation.
var evaluation = args is ["--create-evaluation-users", "--database", _, "--confirm", "DEMO"];
if (!evaluation && args is not ["--create-admin", "--confirm", "SENTRA_DEMO_Stage73"])
{
    Console.Error.WriteLine("Use --create-admin --confirm SENTRA_DEMO_Stage73 ou --create-evaluation-users --database SENTRA_DEMO_Avaliacao_<nome> --confirm DEMO somente apos autorizacao.");
    return 2;
}
try
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var aspEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (environment != "Demo" || (aspEnvironment is not null && aspEnvironment != "Demo"))
        throw new InvalidOperationException("Ambiente Demo obrigatorio.");
    var cs = Environment.GetEnvironmentVariable("SENTRA_DEMO_CONNECTION_STRING") ?? "";
    if (evaluation) DemoEvaluationTargetGuard.Validate(environment, cs, args[2]);
    else DemoTargetGuard.Validate(environment, cs, DemoAdminProvisioner.Database);
    var password = Environment.GetEnvironmentVariable("SENTRA_DEMO_ADMIN_PASSWORD");
    if (string.IsNullOrEmpty(password))
    {
        if (Console.IsInputRedirected) throw new InvalidOperationException("Use terminal interativo ou configuracao privada temporaria.");
        password = ReadSecret(evaluation ? "Senha local das contas DEMO S07: " : "Senha do Admin DEMO: ");
        if (password != ReadSecret("Confirme a senha: ")) throw new InvalidOperationException("Confirmacao divergente.");
    }
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    if (evaluation)
    {
        var count = await DemoAdminProvisioner.CreateEvaluationUsersAsync(db, password, environment, true);
        Console.WriteLine($"Contas DEMO S07 inseridas: {count}. Logins: <role-em-minusculas>.s07@sentra.invalid. Senhas existentes preservadas; nenhuma senha ou token será exibido.");
    }
    else
    {
        await DemoAdminProvisioner.CreateAsync(db, password, environment, confirmed: true);
        Console.WriteLine("Admin DEMO criado. Login: admin.demo@sentra.invalid. Nenhuma senha ou token sera exibido.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Provisionamento nao concluido ({ex.GetType().Name}); verifique destino, schema, ausencia de usuarios e requisitos de senha.");
    return 1;
}
finally { Environment.SetEnvironmentVariable("SENTRA_DEMO_ADMIN_PASSWORD", null); }

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}
