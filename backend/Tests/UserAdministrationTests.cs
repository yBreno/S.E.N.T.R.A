using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Api.Security;
using Application.Security;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
using Xunit;
namespace Tests;

public sealed class UserAdministrationTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!; private User admin = null!;
    private const string Password = "ContasSomenteTeste@2026";
    private static readonly string Hash = new BCryptPasswordHasher().Hash(Password);
    private string AdminToken => host.Token(UserRole.Admin, admin.Id);
    public async Task InitializeAsync() { host = await EquipmentTestHost.StartAsync(validateCurrentUser: true, allowPublicRegistration: false); admin = await Seed(UserRole.Admin); }
    public Task DisposeAsync() => host.DisposeAsync().AsTask();
    private async Task<User> Seed(UserRole role, bool active = true)
    {
        using var scope = host.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create($"DEMO {role}", $"{Guid.NewGuid():N}@sentra.invalid", "00000000000", Hash, role);
        user.SetActive(active); db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private async Task<User> Stored(Guid id)
    {
        using var scope = host.App.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? token = null, bool anonymous = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? AdminToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await host.Client.SendAsync(request);
    }
    private static object Create(object? role = null, string email = "nova@sentra.invalid", string cpf = "52998224725") =>
        new { name = "DEMO Nova conta", email, cpf, password = Password, role = role ?? "Agent" };
    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Theory]
    [InlineData(UserRole.OperationManager)] [InlineData(UserRole.CentralOperator)] [InlineData(UserRole.MaintenanceTechnician)] [InlineData(UserRole.Agent)] [InlineData(null)]
    public async Task Todas_as_operacoes_administrativas_exigem_Admin_no_backend(UserRole? role)
    {
        var user = role.HasValue ? await Seed(role.Value) : null; var token = user is null ? null : host.Token(user.Role, user.Id);
        var id = admin.Id;
        foreach (var (method, path, body) in new (HttpMethod, string, object?)[] {
            (HttpMethod.Get, "/api/users", null), (HttpMethod.Get, $"/api/users/{id}", null), (HttpMethod.Get, $"/api/users/{id}/history", null),
            (HttpMethod.Post, "/api/users", Create()), (HttpMethod.Put, $"/api/users/{id}", new { name = "DEMO", email = "outro@sentra.invalid", revision = 1 }),
            (HttpMethod.Put, $"/api/users/{id}/role", new { role = "Agent", revision = 1 }), (HttpMethod.Put, $"/api/users/{id}/status", new { isActive = false, revision = 1 }) })
        {
            using var response = await Send(method, path, body, token, user is null); Assert.Equal(user is null ? 401 : 403, (int)response.StatusCode);
        }
        using var legacy = await Send(HttpMethod.Post, "/api/auth/register", Create("Admin"), token, user is null);
        Assert.Equal(HttpStatusCode.Forbidden, legacy.StatusCode);
    }
    [Theory]
    [InlineData("Admin")] [InlineData("OperationManager")] [InlineData("CentralOperator")] [InlineData("MaintenanceTechnician")] [InlineData("Agent")]
    public async Task Admin_cria_os_cinco_cargos_com_auditoria_sem_campos_confidenciais(string role)
    {
        using var created = await Send(HttpMethod.Post, "/api/users", Create(role)); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Json(created); var account = body.GetProperty("account"); var id = account.GetProperty("id").GetGuid();
        Assert.Equal(role, account.GetProperty("role").GetString()); Assert.True(account.GetProperty("isActive").GetBoolean());
        foreach (var secret in new[] { "password", "passwordHash", "cpf", "authVersion", "token" }) Assert.False(account.TryGetProperty(secret, out _));
        using var history = await Send(HttpMethod.Get, $"/api/users/{id}/history"); var audit = (await Json(history)).GetProperty("items");
        Assert.Single(audit.EnumerateArray()); Assert.Equal("Created", audit[0].GetProperty("action").GetString());
        Assert.Equal(admin.Id, audit[0].GetProperty("actorUserId").GetGuid());
        Assert.EndsWith("Z", audit[0].GetProperty("createdAt").GetString());
    }
    [Fact]
    public async Task Busca_filtros_paginacao_e_vinculo_profissional_sao_consultas_reais()
    {
        for (var i = 0; i < 12; i++) await Seed(UserRole.Agent);
        using var first = await Send(HttpMethod.Get, "/api/users?search=demo&role=Agent&isActive=true&page=1&pageSize=5");
        var one = await Json(first); Assert.Equal(12, one.GetProperty("total").GetInt32()); Assert.Equal(5, one.GetProperty("items").GetArrayLength());
        using var second = await Send(HttpMethod.Get, "/api/users?role=Agent&page=2&pageSize=5"); var two = await Json(second);
        Assert.DoesNotContain(two.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()), id => one.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == id));
        Guid agentId;
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var agent = new Agent { Name = "DEMO Campo", UserId = admin.Id, Role = AgentRole.PoliceOfficer }; agentId = agent.Id; db.Agents.Add(agent); await db.SaveChangesAsync();
        }
        using var details = await Send(HttpMethod.Get, $"/api/users/{admin.Id}"); Assert.Equal(agentId, (await Json(details)).GetProperty("agentId").GetGuid());
    }
    [Fact]
    public async Task Editar_perfil_preserva_uniquidade_e_impede_escrita_com_revisao_antiga()
    {
        var user = await Seed(UserRole.Agent); var other = await Seed(UserRole.Agent);
        using var duplicate = await Send(HttpMethod.Put, $"/api/users/{user.Id}", new { name = "DEMO", email = other.Email, revision = 1 }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var changed = await Send(HttpMethod.Put, $"/api/users/{user.Id}", new { name = "DEMO Editada", email = "EDITADA@sentra.invalid", revision = 1 }); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var stale = await Send(HttpMethod.Put, $"/api/users/{user.Id}", new { name = "Perda de atualização", email = "nova@sentra.invalid", revision = 1 }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var stored = await Stored(user.Id); Assert.Equal("editada@sentra.invalid", stored.Email); Assert.Equal(2, stored.Revision); Assert.Equal("DEMO Editada", stored.Name);
        using var history = await Send(HttpMethod.Get, $"/api/users/{user.Id}/history"); Assert.Equal(1, (await Json(history)).GetProperty("total").GetInt32());
    }
    [Fact]
    public async Task Atualizacoes_simultaneas_da_mesma_revisao_nao_perdem_dados()
    {
        var user = await Seed(UserRole.Agent);
        var results = await Task.WhenAll(
            Send(HttpMethod.Put, $"/api/users/{user.Id}", new { name = "DEMO alteração A", email = user.Email, revision = 1 }),
            Send(HttpMethod.Put, $"/api/users/{user.Id}", new { name = "DEMO alteração B", email = user.Email, revision = 1 }));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in results) response.Dispose();
        Assert.Equal(2, (await Stored(user.Id)).Revision);
    }
    [Theory]
    [InlineData("role")] [InlineData("status")]
    public async Task Ultimo_Admin_ativo_nao_pode_ser_rebaixado_ou_desativado(string operation)
    {
        object body = operation == "role" ? new { role = "Agent", revision = 1 } : new { isActive = false, revision = 1 };
        using var response = await Send(HttpMethod.Put, $"/api/users/{admin.Id}/{operation}", body); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await Stored(admin.Id); Assert.True(stored.IsActive); Assert.Equal(UserRole.Admin, stored.Role); Assert.Equal(1, stored.AuthVersion);
    }
    [Fact]
    public async Task Administrador_inativo_nao_conta_para_a_regra_do_ultimo_Admin()
    {
        await Seed(UserRole.Admin, active: false);
        using var response = await Send(HttpMethod.Put, $"/api/users/{admin.Id}/status", new { isActive = false, revision = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Rebaixamentos_concorrentes_preservam_um_Admin_ativo()
    {
        var second = await Seed(UserRole.Admin);
        var operations = new[] { Send(HttpMethod.Put, $"/api/users/{admin.Id}/role", new { role = "Agent", revision = 1 }),
            Send(HttpMethod.Put, $"/api/users/{second.Id}/role", new { role = "Agent", revision = 1 }, host.Token(UserRole.Admin, second.Id)) };
        var results = await Task.WhenAll(operations); Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);
        foreach (var response in results) response.Dispose();
        using var scope = host.App.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive));
    }
    [Fact]
    public async Task Desativacao_reativacao_e_ciclo_de_cargo_nao_restauram_JWTs_antigos()
    {
        var user = await Seed(UserRole.MaintenanceTechnician); var original = host.Token(user.Role, user.Id);
        using var inactive = await Send(HttpMethod.Put, $"/api/users/{user.Id}/status", new { isActive = false, revision = 1 }); Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        using var loginInactive = await Send(HttpMethod.Post, "/api/auth/login", new { email = user.Email, password = Password }, anonymous: true); Assert.Equal(HttpStatusCode.Unauthorized, loginInactive.StatusCode);
        using var old = await Send(HttpMethod.Get, "/api/equipments", token: original); Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var active = await Send(HttpMethod.Put, $"/api/users/{user.Id}/status", new { isActive = true, revision = 2 }); Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        using var stillOld = await Send(HttpMethod.Get, "/api/equipments", token: original); Assert.Equal(HttpStatusCode.Unauthorized, stillOld.StatusCode);
        using var role = await Send(HttpMethod.Put, $"/api/users/{user.Id}/role", new { role = "Agent", revision = 3 }); Assert.Equal(HttpStatusCode.OK, role.StatusCode);
        using var back = await Send(HttpMethod.Put, $"/api/users/{user.Id}/role", new { role = "MaintenanceTechnician", revision = 4 }); Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        using var revoked = await Send(HttpMethod.Get, "/api/equipments", token: original); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var fresh = await Send(HttpMethod.Get, "/api/equipments", token: host.Token(user.Role, user.Id, authVersion: 5)); Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }
    [Fact]
    public async Task Alterar_senha_do_titular_e_auditavel_e_revoga_todas_as_sessoes()
    {
        var user = await Seed(UserRole.MaintenanceTechnician); var token = host.Token(user.Role, user.Id);
        using var wrong = await Send(HttpMethod.Post, "/api/auth/change-password", new { currentPassword = "Errada@123", newPassword = "NovaPrivada@2026" }, token); Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var changed = await Send(HttpMethod.Post, "/api/auth/change-password", new { currentPassword = Password, newPassword = "NovaPrivada@2026" }, token); Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var old = await Send(HttpMethod.Get, "/api/equipments", token: token); Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var oldLogin = await Send(HttpMethod.Post, "/api/auth/login", new { email = user.Email, password = Password }, anonymous: true); Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await Send(HttpMethod.Post, "/api/auth/login", new { email = user.Email, password = "NovaPrivada@2026" }, anonymous: true); Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var history = await Send(HttpMethod.Get, $"/api/users/{user.Id}/history"); var text = await history.Content.ReadAsStringAsync(); Assert.Contains("PasswordChanged", text); Assert.DoesNotContain("NovaPrivada", text); Assert.DoesNotContain("passwordHash", text);
    }
    [Fact]
    public async Task Tokens_legados_sem_versao_e_versoes_falsas_sao_recusados()
    {
        foreach (var version in new string?[] { null, "0", "-1", "2", "not-a-version" })
        {
            var claims = new List<Claim> { new("sub", admin.Id.ToString()), new("role", "Admin") }; if (version is not null) claims.Add(new("auth_version", version));
            using var response = await Send(HttpMethod.Get, "/api/users", token: host.SignedToken(claims)); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
    [Theory]
    [InlineData("PoliceOfficer")] [InlineData("SuperAdmin")] [InlineData(99)]
    public async Task Cargo_desconhecido_nao_e_persistido(object role)
    {
        using var response = await Send(HttpMethod.Post, "/api/users", Create(role)); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var list = await Send(HttpMethod.Get, "/api/users"); Assert.Equal(1, (await Json(list)).GetProperty("total").GetInt32());
    }
    [Theory]
    [InlineData("Production", true, false)] [InlineData("production", true, false)] [InlineData("Production", false, true)]
    [InlineData("Testing", true, true)] [InlineData("Development", true, true)] [InlineData("Unknown", true, false)]
    public void Auto_cadastro_tem_padrao_fechado_e_so_permite_opt_in_em_desenvolvimento_ou_testes(string environment, bool allow, bool expected)
    {
        Assert.False(new PublicRegistrationSettings().AllowAgentSelfRegistration); Assert.Equal(expected, PublicRegistrationSettings.IsSafe(environment, allow));
    }
    [Fact]
    public async Task Provisionamento_administrativo_funciona_com_auto_cadastro_fechado()
    {
        using var publicRegistration = await Send(HttpMethod.Post, "/api/auth/register", Create(), anonymous: true); Assert.Equal(HttpStatusCode.Forbidden, publicRegistration.StatusCode);
        using var administrative = await Send(HttpMethod.Post, "/api/users", Create()); Assert.Equal(HttpStatusCode.Created, administrative.StatusCode);
        using var duplicate = await Send(HttpMethod.Post, "/api/users", Create()); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var pageInvalid = await Send(HttpMethod.Get, "/api/users?pageSize=1000"); Assert.Equal(HttpStatusCode.BadRequest, pageInvalid.StatusCode);
        using var invalidRole = await Send(HttpMethod.Put, $"/api/users/{admin.Id}/role", new { revision = 1 }); Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
    }
}
