using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Application.Security;
using Infrastructure.Demo;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;

namespace Tests;

public class RbacSecurityTests
{
    private static async Task<User> SeedUser(EquipmentTestHost host, UserRole role)
    {
        using var scope = host.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create("Conta sintética RBAC", $"{Guid.NewGuid():N}@sentra.invalid", "00000000000", "test-only", role);
        db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static async Task<HttpResponseMessage> Send(EquipmentTestHost host, string path, string? token, HttpMethod? method = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await host.Client.SendAsync(request);
    }
    private static object Registration(object? role = null) => new { name = "Cadastro sintético", email = "cadastro@sentra.invalid", cpf = "52998224725", password = "ApenasTeste@2026", role };

    [Theory]
    [InlineData(UserRole.Admin)] [InlineData(UserRole.OperationManager)] [InlineData(UserRole.CentralOperator)]
    [InlineData(UserRole.MaintenanceTechnician)] [InlineData(UserRole.Agent)] [InlineData(null)]
    public async Task Matriz_HTTP_usa_identidade_atual_e_bloqueia_acesso_direto_sem_permissao(UserRole? role)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var token = role.HasValue ? host.Token(role.Value, (await SeedUser(host, role.Value)).Id) : null;
        var monitoring = role is UserRole.Admin or UserRole.OperationManager or UserRole.CentralOperator;
        var staff = monitoring || role == UserRole.MaintenanceTechnician;
        var maintenance = role is UserRole.Admin or UserRole.OperationManager or UserRole.MaintenanceTechnician;
        foreach (var (path, allowed) in new[] {
            ("/api/equipments", staff), ("/api/addresses", staff), ("/api/alerts", staff), ("/api/recommendations", staff),
            ("/api/dashboard?includePredictive=false", monitoring), ("/api/agents", monitoring), ("/api/risk", monitoring),
            ("/api/predictions/equipment-risk", staff), ("/api/predictions/failures", staff),
            ("/api/predictions/anomalies", monitoring), ("/api/maintenances", maintenance) })
        {
            using var response = await Send(host, path, token);
            Assert.Equal(role is null ? 401 : allowed ? 200 : 403, (int)response.StatusCode);
        }
        using var me = await Send(host, "/api/agents/me", token);
        Assert.Equal(role is null ? 401 : 404, (int)me.StatusCode); // Autenticado, mas sem vínculo profissional.
        var manager = role is UserRole.Admin or UserRole.OperationManager;
        foreach (var (path, allowed) in new[] { ("/api/equipments", manager), ("/api/agents", manager), ("/api/maintenances/schedule", maintenance) })
        {
            using var response = await Send(host, path, token, HttpMethod.Post, new { });
            Assert.Equal(role is null ? 401 : allowed ? 400 : 403, (int)response.StatusCode);
        }
        using var accept = await Send(host, $"/api/recommendations/{Guid.NewGuid()}/accept", token, HttpMethod.Post);
        Assert.Equal(role is null ? 401 : manager ? 404 : 403, (int)accept.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Admin, 201)] [InlineData(UserRole.OperationManager, 403)] [InlineData(UserRole.CentralOperator, 403)]
    [InlineData(UserRole.MaintenanceTechnician, 403)] [InlineData(UserRole.Agent, 403)] [InlineData(null, 403)]
    public async Task Apenas_Admin_atual_pode_criar_usuario_com_cargo_elevado(UserRole? role, int expected)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var token = role.HasValue ? host.Token(role.Value, (await SeedUser(host, role.Value)).Id) : null;
        using var response = await Send(host, "/api/auth/register", token, HttpMethod.Post, Registration("Admin"));
        Assert.Equal(expected, (int)response.StatusCode);
        using var scope = host.App.Services.CreateScope();
        Assert.Equal((role.HasValue ? 1 : 0) + (expected == 201 ? 1 : 0), await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync());
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("52998224725", json); Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cadastro_publico_compatível_continua_Agent_e_nao_expoe_senha_ou_documento()
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        using var registration = await Send(host, "/api/auth/register", null, HttpMethod.Post, Registration());
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var response = JsonDocument.Parse(await registration.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Agent", response.GetProperty("role").GetString());
        Assert.False(response.TryGetProperty("cpf", out _)); Assert.False(response.TryGetProperty("passwordHash", out _));
        using var login = await Send(host, "/api/auth/login", null, HttpMethod.Post, new { email = "cadastro@sentra.invalid", password = "ApenasTeste@2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        using var forbidden = await Send(host, "/api/equipments", session.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("PoliceOfficer")] [InlineData("SuperAdmin")] [InlineData(99)]
    public async Task Cadastro_rejeita_categorias_profissionais_e_cargos_inexistentes(object role)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        using var response = await Send(host, "/api/auth/register", null, HttpMethod.Post, Registration(role));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cargo_alterado_e_usuario_removido_invalidam_o_JWT_existente_sem_mudar_schema()
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var user = await SeedUser(host, UserRole.Admin); var token = host.Token(user.Role, user.Id);
        using var before = await Send(host, "/api/equipments", token); Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.Users.SingleAsync()).ChangeRole(UserRole.Agent); await db.SaveChangesAsync();
        }
        using var stale = await Send(host, "/api/equipments", token); Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        using var elevation = await Send(host, "/api/auth/register", token, HttpMethod.Post, Registration("Admin"));
        Assert.Equal(HttpStatusCode.Forbidden, elevation.StatusCode); // AllowAnonymous nunca transforma token rejeitado em Admin.
        var current = host.Token(UserRole.Agent, user.Id, authVersion: 2);
        using var limited = await Send(host, "/api/equipments", current); Assert.Equal(HttpStatusCode.Forbidden, limited.StatusCode);
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Users.Remove(await db.Users.SingleAsync()); await db.SaveChangesAsync();
        }
        using var deleted = await Send(host, "/api/agents/me", current); Assert.Equal(HttpStatusCode.Unauthorized, deleted.StatusCode);
    }

    [Theory]
    [InlineData("unknown-user")] [InlineData("invalid-sub")] [InlineData("professional-role")] [InlineData("multiple-roles")] [InlineData("numeric-role")]
    public async Task JWT_assinado_mas_sem_identidade_canonica_nao_autentica(string invalid)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var user = await SeedUser(host, UserRole.Admin);
        var claims = new List<Claim> { new("sub", invalid == "invalid-sub" ? "not-a-guid" : invalid == "unknown-user" ? Guid.NewGuid().ToString() : user.Id.ToString()),
            new("role", invalid == "professional-role" ? "PoliceOfficer" : invalid == "numeric-role" ? "0" : "Admin") };
        if (invalid == "multiple-roles") claims.Add(new("role", "Agent"));
        using var response = await Send(host, "/api/equipments", host.SignedToken(claims));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("signature")] [InlineData("expired")] [InlineData("issuer")] [InlineData("audience")]
    public async Task Validacao_do_usuario_nao_substitui_validacao_criptografica_do_JWT(string invalid)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var user = await SeedUser(host, UserRole.Admin);
        Claim[] claims = [new("sub", user.Id.ToString()), new("role", "Admin")];
        var token = host.SignedToken(claims, expires: invalid == "expired" ? DateTime.UtcNow.AddMinutes(-2) : null,
            issuer: invalid == "issuer" ? "other-issuer" : null, audience: invalid == "audience" ? "other-audience" : null);
        if (invalid == "signature")
        {
            var parts = token.Split('.'); parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..]; token = string.Join('.', parts);
        }
        using var response = await Send(host, "/api/equipments", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory]
    [InlineData("/api/auth/register")] [InlineData("/api/auth/login")]
    public async Task Senha_acima_do_limite_retorna_validacao_sem_reproduzir_seu_valor(string path)
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        var password = new string('é', 40) + "SufixoPrivado@1";
        using var response = await Send(host, path, null, HttpMethod.Post,
            new { name = "DEMO", email = "demo@sentra.invalid", cpf = "52998224725", password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("SufixoPrivado", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Perfil_demo_preserva_login_aceite_idempotente_e_consultas_com_usuario_atual()
    {
        await using var host = await EquipmentTestHost.StartAsync(demo: true, interactiveDemo: true, validateCurrentUser: true);
        const string password = "SomenteTesteDemo@2026";
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DemoDataLoader.ApplyAsync(db, DateTime.UtcNow, "Demo", true, profile: DemoDataProfile.Evaluation);
            await DemoAdminProvisioner.CreateEvaluationUsersAsync(db, password, "Demo", true);
        }
        using var login = await Send(host, "/api/auth/login", null, HttpMethod.Post, new { email = "operationmanager.s07@sentra.invalid", password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        using var dashboard = await Send(host, "/api/dashboard", token); Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        for (var i = 0; i < 2; i++)
        {
            using var accept = await Send(host, $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId}/accept", token, HttpMethod.Post);
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        }
        using var blocked = await Send(host, "/api/predictions/analyze", token, HttpMethod.Post); Assert.Equal(HttpStatusCode.MethodNotAllowed, blocked.StatusCode);
        using var final = host.App.Services.CreateScope();
        Assert.Equal(10, await final.ServiceProvider.GetRequiredService<AppDbContext>().MaintenanceRecords.CountAsync());
    }
    [Fact]
    public async Task Cargo_invalido_em_dado_legado_nao_emite_token_no_login()
    {
        await using var host = await EquipmentTestHost.StartAsync(validateCurrentUser: true);
        const string password = "SomenteTeste@2026";
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = User.Create("DEMO", "legado@sentra.invalid", "00000000000", new BCryptPasswordHasher().Hash(password), UserRole.Agent);
            db.Users.Add(user); db.Entry(user).Property(nameof(User.Role)).CurrentValue = (UserRole)99; await db.SaveChangesAsync();
        }
        using var login = await Send(host, "/api/auth/login", null, HttpMethod.Post, new { email = "legado@sentra.invalid", password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain("token", await login.Content.ReadAsStringAsync());
    }
}
