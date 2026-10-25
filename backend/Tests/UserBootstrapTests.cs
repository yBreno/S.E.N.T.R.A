using Api.Seed;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.Support;
namespace Tests;

public class UserBootstrapTests
{
    [Fact]
    public async Task Primeiro_Admin_exige_configuracao_privada_valida_e_e_idempotente_concorrente_e_auditavel()
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true, allowPublicRegistration: false);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["SeedAdmin:Name"] = "DEMO Bootstrap", ["SeedAdmin:Email"] = "bootstrap@sentra.invalid", ["SeedAdmin:Cpf"] = "52998224725", ["SeedAdmin:Password"] = "weak" }).Build();
        var logger = new CapturingLogger(); await AdminSeeder.SeedAsync(host.App.Services, config, logger);
        using (var scope = host.App.Services.CreateScope()) Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ToListAsync());
        config["SeedAdmin:Password"] = "BootstrapPrivado@2026";
        await Task.WhenAll(AdminSeeder.SeedAsync(host.App.Services, config, logger), AdminSeeder.SeedAsync(host.App.Services, config, logger));
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = Assert.Single(await db.Users.ToListAsync());
            Assert.Equal(UserRole.Admin, user.Role); Assert.True(user.IsActive); Assert.Equal(1, user.AuthVersion);
            var audit = Assert.Single(await db.UserAuditEvents.ToListAsync()); Assert.Equal("InitialProvisioning", audit.Action); Assert.Null(audit.ActorUserId); Assert.Equal(user.Id, audit.UserId);
        }
        var logs = string.Join(' ', logger.Messages); Assert.DoesNotContain("BootstrapPrivado", logs); Assert.DoesNotContain("bootstrap@sentra.invalid", logs); Assert.DoesNotContain("52998224725", logs);
    }
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
