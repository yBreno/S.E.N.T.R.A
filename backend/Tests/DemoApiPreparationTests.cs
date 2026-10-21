using System.Security.Cryptography;
using System.Text.Json;
using Api.Demo;
using Api.Workers;
using Application.DTOs.Auth;
using Application.Security;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Demo;
using Infrastructure.Repositories;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tests;

// No WebApplication/Host is started. Every persistence test uses a unique InMemory store.
public class DemoApiPreparationTests
{
    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static string Password() => Key() + "aA1!";
    private static ConfigurationManager Settings()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Demo:Enabled"] = "true", ["Simulation:Enabled"] = "false", ["SeedAdmin:Enabled"] = "false",
            ["urls"] = DemoApiConfiguration.Url, ["SENTRA_DEMO_JWT_KEY"] = Key(),
            ["SENTRA_DEMO_CONNECTION_STRING"] = new SqlConnectionStringBuilder
            {
                DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = DemoApiConfiguration.Database, IntegratedSecurity = true
            }.ConnectionString,
            ["ConnectionStrings:DefaultConnection"] = "NOT-A-CONNECTION-DO-NOT-USE", ["Jwt:Key"] = "DO-NOT-USE"
        });
        return config;
    }

    [Fact]
    public void Demo_is_neither_Development_nor_Production_and_has_dedicated_connection_and_JWT()
    {
        var config = Settings();
        var environment = new TestEnvironment("Demo");
        Assert.False(environment.IsDevelopment());
        Assert.False(environment.IsProduction());
        Assert.True(DemoApiConfiguration.Prepare(config, environment));
        Assert.Equal(config["SENTRA_DEMO_CONNECTION_STRING"], config.GetConnectionString("DefaultConnection"));
        Assert.True(config["Jwt:Key"] == config["SENTRA_DEMO_JWT_KEY"]);
        Assert.Equal(DemoApiConfiguration.Issuer, config["Jwt:Issuer"]);
        Assert.Equal(DemoApiConfiguration.Audience, config["Jwt:Audience"]);
        Assert.Equal("30", config["Jwt:ExpiresMinutes"]);
        Assert.Equal("localhost;127.0.0.1", config["AllowedHosts"]);
        Assert.Equal(new[] { "http://localhost:5173", "http://127.0.0.1:5173" }, DemoApiConfiguration.Origins);
    }

    [Theory]
    [InlineData("Demo:Enabled", "false")]
    [InlineData("Simulation:Enabled", "true")]
    [InlineData("Simulation:Enabled", null)]
    [InlineData("SeedAdmin:Enabled", "true")]
    [InlineData("SENTRA_DEMO_CONNECTION_STRING", null)]
    [InlineData("SENTRA_DEMO_JWT_KEY", null)]
    [InlineData("SENTRA_DEMO_JWT_KEY", "not-base64")]
    [InlineData("SENTRA_DEMO_JWT_KEY", "YWJjZA==")]
    [InlineData("urls", "http://0.0.0.0:5180")]
    [InlineData("urls", "http://127.0.0.1:5180;http://localhost:5129")]
    [InlineData("HTTP_PORTS", "80")]
    [InlineData("HTTPS_PORTS", "443")]
    [InlineData("Kestrel:Endpoints:External:Url", "http://0.0.0.0:80")]
    public void Unsafe_or_incomplete_Demo_configuration_is_rejected_without_connecting(string name, string? value)
    {
        var config = Settings();
        config[name] = value;
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(config, new TestEnvironment("Demo")));
    }

    [Theory]
    [InlineData("Server", "example.invalid")]
    [InlineData("Database", "SENTRA_DEMO_Other")]
    [InlineData("Database", "master")]
    [InlineData("Integrated Security", "false")]
    public void Wrong_target_is_rejected_before_database_registration(string name, string value)
    {
        var config = Settings();
        var cs = new SqlConnectionStringBuilder(config["SENTRA_DEMO_CONNECTION_STRING"]);
        cs[name] = value;
        config["SENTRA_DEMO_CONNECTION_STRING"] = cs.ConnectionString;
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(config, new TestEnvironment("Demo")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Demo_cannot_be_enabled_under_another_environment(string environment) =>
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(Settings(), new TestEnvironment(environment)));

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Dedicated_Demo_variables_cannot_fall_back_to_another_environment_even_when_flag_is_disabled(string environment)
    {
        var config = Settings();
        config["Demo:Enabled"] = "false";
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(config, new TestEnvironment(environment)));
    }

    [Fact]
    public void Existing_non_Demo_configuration_is_preserved_and_Demo_registers_no_simulation_worker_or_seeders()
    {
        var normal = new ConfigurationManager();
        normal["ConnectionStrings:DefaultConnection"] = "UNCHANGED-NO-CONNECTION";
        Assert.False(DemoApiConfiguration.Prepare(normal, new TestEnvironment("Production")));
        Assert.Equal("UNCHANGED-NO-CONNECTION", normal.GetConnectionString("DefaultConnection"));
        var services = new ServiceCollection();
        DemoApiConfiguration.RegisterSimulation(services, demo: true);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
        DemoApiConfiguration.RegisterSimulation(services, demo: false);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TelemetrySimulationWorker));
        Assert.False(DemoApiConfiguration.RunDevelopmentSeeders(new TestEnvironment("Demo"), demo: true));
        var config = Settings();
        config["SeedAdmin:Enabled"] = "true";
        Assert.False(DemoApiConfiguration.RunAdminSeeder(config, demo: true));
        Assert.True(DemoApiConfiguration.RunDevelopmentSeeders(new TestEnvironment("Development"), demo: false));
        Assert.True(DemoApiConfiguration.RunAdminSeeder(config, demo: false));
    }

    [Theory]
    [InlineData("GET", "/api/equipments", true)]
    [InlineData("HEAD", "/api/dashboard", true)]
    [InlineData("OPTIONS", "/api/agents", true)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/API/AUTH/LOGIN/", true)]
    [InlineData("POST", "/api/auth/register", false)]
    [InlineData("POST", "/api/incidents", false)]
    [InlineData("PUT", "/api/agents/fixture/location", false)]
    [InlineData("PATCH", "/api/equipments/fixture/status", false)]
    [InlineData("DELETE", "/api/equipments/fixture", false)]
    public async Task Demo_HTTP_allows_only_reads_preflight_and_existing_login(string method, string path, bool allowed)
    {
        var called = false;
        var middleware = new DemoReadOnlyMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        Assert.Equal(allowed, called);
        if (!allowed) Assert.Equal(405, context.Response.StatusCode);
    }

    [Fact]
    public async Task Demo_interceptor_blocks_sync_and_async_SaveChanges_before_any_persistence()
    {
        var name = $"DEMO-READONLY-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name)
            .AddInterceptors(new DemoReadOnlyInterceptor()).Options;
        await using var db = new AppDbContext(options);
        db.Addresses.Add(new Address { Street = "DEMO test only" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var check = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);
        Assert.Empty(await check.Addresses.ToListAsync());
    }

    [Fact]
    public void Actual_persistence_registration_has_readonly_interceptor_only_in_Demo_and_never_opens_connection()
    {
        foreach (var demo in new[] { false, true })
        {
            var config = Settings();
            DemoApiConfiguration.Prepare(config, new TestEnvironment("Demo"));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(config);
            DemoApiConfiguration.RegisterReadOnlyPersistence(services, demo);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // EF may append Application Name; compare the safety-critical fields, not textual formatting.
            var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            Assert.Equal(@"(localdb)\MSSQLLocalDB", actual.DataSource);
            Assert.Equal(DemoApiConfiguration.Database, actual.InitialCatalog);
            Assert.True(actual.IntegratedSecurity);
            DemoTargetGuard.Validate("Demo", actual.ConnectionString, DemoApiConfiguration.Database);
            Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
            var interceptors = db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().Interceptors;
            Assert.Equal(demo, interceptors?.Any(i => i is DemoReadOnlyInterceptor) ?? false);
        }
    }

    [Fact]
    public async Task Manual_bootstrap_adds_one_fixture_Admin_and_preserves_all_operational_scalar_values()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DEMO-ADMIN-TEST-{Guid.NewGuid():N}").Options);
        await DemoDataLoader.ApplyAsync(db, DateTime.UtcNow, "Demo", confirmed: true);
        string Snapshot()
        {
            db.Addresses.Load(); db.Equipments.Load(); db.Agents.Load(); db.Incidents.Load(); db.Alerts.Load(); db.MaintenanceRecords.Load();
            return JsonSerializer.Serialize(db.ChangeTracker.Entries().Where(e => e.Entity is not User)
                .OrderBy(e => ((BaseEntity)e.Entity).Id).Select(e => e.Properties.OrderBy(p => p.Metadata.Name)
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)));
        }
        var before = Snapshot();
        var password = Password();
        await DemoAdminProvisioner.CreateAsync(db, password, "Demo", confirmed: true);
        var admin = Assert.Single(await db.Users.ToListAsync());
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(DemoAdminProvisioner.Email, admin.Email);
        Assert.True(new BCryptPasswordHasher().Verify(password, admin.PasswordHash));
        Assert.Equal(before, Snapshot());
        var hash = admin.PasswordHash;
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoAdminProvisioner.CreateAsync(db, Password(), "Demo", confirmed: true));
        Assert.Single(await db.Users.ToListAsync());
        Assert.True((await db.Users.SingleAsync()).PasswordHash == hash);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Existing_login_works_with_readonly_context_and_returns_only_Demo_signed_token_in_memory()
    {
        var name = $"DEMO-LOGIN-TEST-{Guid.NewGuid():N}";
        var password = Password();
        await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options))
            await DemoAdminProvisioner.CreateAsync(setup, password, "Demo", confirmed: true);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name)
            .AddInterceptors(new DemoReadOnlyInterceptor()).Options);
        var signingKey = Key();
        var tokens = new JwtTokenService(Options.Create(new JwtSettings
        {
            Key = signingKey, Issuer = DemoApiConfiguration.Issuer, Audience = DemoApiConfiguration.Audience, ExpiresMinutes = 30
        }));
        var service = new AuthService(new UserRepository(db), db, new BCryptPasswordHasher(), tokens,
            new RegisterDtoValidator(), new LoginDtoValidator());
        var response = await service.LoginAsync(new LoginDto { Email = DemoAdminProvisioner.Email, Password = password });
        Assert.True(!string.IsNullOrEmpty(response.Token));
        Assert.Equal(UserRole.Admin, response.User.Role);
        Assert.False(db.ChangeTracker.HasChanges());
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Equal(DemoApiConfiguration.Issuer, token.Issuer);
        Assert.Contains(DemoApiConfiguration.Audience, token.Audiences);
        var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = DemoApiConfiguration.Issuer,
            ValidateAudience = true, ValidAudience = DemoApiConfiguration.Audience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey))
        };
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        handler.ValidateToken(response.Token, parameters, out _);
        parameters.IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Key()));
        Assert.ThrowsAny<Microsoft.IdentityModel.Tokens.SecurityTokenException>(() => handler.ValidateToken(response.Token, parameters, out _));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Demo", false)]
    public async Task Manual_bootstrap_requires_Demo_and_explicit_confirmation(string environment, bool confirmed)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DEMO-GUARD-TEST-{Guid.NewGuid():N}").Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoAdminProvisioner.CreateAsync(db, Password(), environment, confirmed));
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public void Oversized_BCrypt_password_is_rejected_without_hashing_or_database_access() =>
        Assert.Throws<InvalidOperationException>(() => DemoAdminProvisioner.ValidatePassword(new string('A', 73)));

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Api";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
