using System.Text;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;

// Manual provisioning only. No API startup, seeders, migrations, or JWT generation.
if (args is not ["--create-admin", "--confirm", "SENTRA_DEMO_Stage73"])
{
    Console.Error.WriteLine("Use --create-admin --confirm SENTRA_DEMO_Stage73 somente apos autorizacao.");
    return 2;
}
try
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var aspEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (environment != "Demo" || (aspEnvironment is not null && aspEnvironment != "Demo"))
        throw new InvalidOperationException("Ambiente Demo obrigatorio.");
    var cs = Environment.GetEnvironmentVariable("SENTRA_DEMO_CONNECTION_STRING") ?? "";
    DemoTargetGuard.Validate(environment, cs, DemoAdminProvisioner.Database);
    var password = Environment.GetEnvironmentVariable("SENTRA_DEMO_ADMIN_PASSWORD");
    if (string.IsNullOrEmpty(password))
    {
        if (Console.IsInputRedirected) throw new InvalidOperationException("Use terminal interativo ou configuracao privada temporaria.");
        password = ReadSecret("Senha do Admin DEMO: ");
        if (password != ReadSecret("Confirme a senha: ")) throw new InvalidOperationException("Confirmacao divergente.");
    }
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    await DemoAdminProvisioner.CreateAsync(db, password, environment, confirmed: true);
    Console.WriteLine("Admin DEMO criado. Login: admin.demo@sentra.invalid. Nenhuma senha ou token sera exibido.");
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
