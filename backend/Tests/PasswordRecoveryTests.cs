using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Application.Security;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tests.Support;
namespace Tests;

public sealed class PasswordRecoveryTests : IAsyncLifetime
{
    private EquipmentTestHost host = null!;
    private readonly RecoveryClock clock = new();
    private const string OldPassword = "TesteAnterior@2026";
    private const string NewPassword = "TesteRenovada@2026";
    private static readonly string Hash = new BCryptPasswordHasher().Hash(OldPassword);
    private LocalTestPasswordRecoverySender Mail => host.App.Services.GetRequiredService<LocalTestPasswordRecoverySender>();
    public async Task InitializeAsync() => host = await EquipmentTestHost.StartAsync(validateCurrentUser: true, allowPublicRegistration: false, passwordRecovery: true,
        configure: services => services.AddScoped<IPasswordRecoveryService>(sp => ActivatorUtilities.CreateInstance<PasswordRecoveryService>(sp, clock)));
    public Task DisposeAsync() => host.DisposeAsync().AsTask();
    private async Task<User> Seed(UserRole role = UserRole.Agent, bool active = true)
    {
        using var scope = host.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create("DEMO recuperação", Guid.NewGuid().ToString("N") + "@sentra.invalid", "00000000000", Hash, role);
        user.SetActive(active); db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private async Task<User> Stored(Guid id)
    {
        using var scope = host.App.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }
    private async Task<HttpResponseMessage> Request(string email) => await host.Client.PostAsJsonAsync("/api/auth/password-recovery/request", new { email });
    private async Task<HttpResponseMessage> Confirm(string secret, string password = NewPassword, string? confirmation = null) =>
        await host.Client.PostAsJsonAsync("/api/auth/password-recovery/confirm", new { token = secret, newPassword = password, confirmPassword = confirmation ?? password });
    private async Task<string> Issue(User user)
    {
        var before = Mail.Messages.Length;
        using var response = await Request(user.Email); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Until(async () =>
        {
            if (Mail.Messages.Length <= before) return false;
            using var scope = host.App.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PasswordResetTokens.AnyAsync(t => t.UserId == user.Id && t.Delivered);
        });
        return Mail.Messages.Last(m => m.Recipient == user.Email).Secret;
    }
    private static async Task Until(Func<Task<bool>> test)
    {
        for (var i = 0; i < 150; i++) { if (await test()) return; await Task.Delay(20); }
        Assert.Fail("Operação assíncrona não terminou no prazo de teste.");
    }
    [Fact]
    public async Task Resposta_publica_identica_existente_inexistente_inativa_e_limitada()
    {
        var active = await Seed(); var inactive = await Seed(active: false);
        using var existing = await Request(active.Email); using var unknown = await Request("inexistente@sentra.invalid"); using var disabled = await Request(inactive.Email);
        var body = await existing.Content.ReadAsStringAsync();
        foreach (var r in new[] { existing, unknown, disabled }) { Assert.Equal(HttpStatusCode.Accepted, r.StatusCode); Assert.Equal(body, await r.Content.ReadAsStringAsync()); }
        for (var i = 0; i < 4; i++) { using var limited = await Request(active.Email); Assert.Equal(body, await limited.Content.ReadAsStringAsync()); }
        Assert.DoesNotContain(active.Email, body); Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        await Issue(await Seed()); Assert.DoesNotContain(Mail.Messages, m => m.Recipient == inactive.Email || m.Recipient == "inexistente@sentra.invalid");
    }
    [Fact]
    public async Task Token_CSPRNG_tem_256_bits_so_hash_persistido_senha_antiga_preservada()
    {
        var user = await Seed(); var secret = await Issue(user);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", secret);
        using var scope = host.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.PasswordResetTokens.SingleAsync();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))), row.TokenHash);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddMinutes(15), row.ExpiresAt);
        Assert.Equal(user.PasswordHash, (await Stored(user.Id)).PasswordHash);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(row));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(await db.UserAuditEvents.ToArrayAsync()));
    }
    [Fact]
    public async Task Confirmacao_unica_atualiza_hash_revoga_JWT_e_audita_sem_credenciais()
    {
        var user = await Seed(UserRole.Admin); var jwt = host.Token(user.Role, user.Id); var secret = await Issue(user);
        using var done = await Confirm(secret); Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        using var reused = await Confirm(secret); Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        var stored = await Stored(user.Id); Assert.Equal(2, stored.AuthVersion); Assert.Equal(2, stored.Revision);
        Assert.True(new BCryptPasswordHasher().Verify(NewPassword, stored.PasswordHash));
        using var oldLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new { user.Email, password = OldPassword }); Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new { user.Email, password = NewPassword }); Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        using var oldJwt = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, oldJwt.StatusCode);
        using var scope = host.App.Services.CreateScope(); var events = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserAuditEvents.ToArrayAsync();
        Assert.Single(events, x => x.Action == "PasswordResetCompleted");
        var json = JsonSerializer.Serialize(events);
        foreach (var value in new[] { secret, OldPassword, NewPassword, stored.PasswordHash, user.Email }) Assert.DoesNotContain(value, json);
    }
    [Fact]
    public async Task Consumo_concorrente_tem_um_unico_vencedor()
    {
        var user = await Seed(); var secret = await Issue(user);
        var results = await Task.WhenAll(Confirm(secret), Confirm(secret));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest); foreach (var r in results) r.Dispose();
        Assert.Equal(2, (await Stored(user.Id)).AuthVersion);
    }
    [Fact]
    public async Task Token_expirado_na_fronteira_nao_muda_senha()
    {
        var user = await Seed(); var secret = await Issue(user); clock.Advance(TimeSpan.FromMinutes(15));
        using var response = await Confirm(secret); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Hash, (await Stored(user.Id)).PasswordHash);
    }
    [Theory]
    [InlineData("invalid")] [InlineData("")] [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Tokens_invalidos_nao_revelam_conta(string token)
    {
        using var response = await Confirm(token); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("@", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Nova_solicitacao_substitui_token_anterior()
    {
        var user = await Seed(); var first = await Issue(user); var second = await Issue(user); Assert.NotEqual(first, second);
        using var previous = await Confirm(first); Assert.Equal(HttpStatusCode.BadRequest, previous.StatusCode);
        using var current = await Confirm(second); Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }
    [Theory]
    [InlineData("inactive")] [InlineData("email")] [InlineData("role")] [InlineData("password")]
    public async Task Mudanca_de_acesso_invalida_link_mesmo_apos_restauracao(string change)
    {
        var user = await Seed(); var secret = await Issue(user);
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var target = await db.Users.SingleAsync();
            if (change == "inactive") { target.SetActive(false); target.SetActive(true); }
            if (change == "email") { target.UpdateProfile(target.Name, "novo@sentra.invalid"); target.UpdateProfile(target.Name, user.Email); }
            if (change == "role") { target.ChangeRole(UserRole.OperationManager); target.ChangeRole(UserRole.Agent); }
            if (change == "password") target.ChangePasswordHash(Hash);
            await db.SaveChangesAsync();
        }
        using var response = await Confirm(secret); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory]
    [InlineData("short", "short")] [InlineData("ForteMasDiferente@2026", "Outra@2026")] [InlineData("aaaaaaaa@2026", "aaaaaaaa@2026")]
    public async Task Senha_invalida_nao_consumiu_token(string password, string confirmation)
    {
        var user = await Seed(); var secret = await Issue(user);
        using var invalid = await Confirm(secret, password, confirmation); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var valid = await Confirm(secret); Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
    }
    [Fact]
    public async Task Limite_BCrypt_UTF8_preservado()
    {
        var user = await Seed(); var secret = await Issue(user); var password = "Aa1@" + new string('á', 35);
        using var response = await Confirm(secret, password); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var valid = await Confirm(secret); Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
    }
    [Fact]
    public async Task Erro_de_confirmacao_e_ToString_nao_ecoam_senha_email_ou_token()
    {
        var secret = new string('F', 43);
        using var response = await Confirm(secret, NewPassword, "ConfirmacaoDiferente@2026");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(NewPassword, body); Assert.DoesNotContain("ConfirmacaoDiferente", body); Assert.DoesNotContain(secret, body);
        Assert.DoesNotContain(NewPassword, new ResetPasswordDto(secret, NewPassword, NewPassword).ToString());
        Assert.DoesNotContain("titular@sentra.invalid", new ForgotPasswordDto("titular@sentra.invalid").ToString());
    }
    [Fact]
    public async Task Falha_de_envio_invalida_token_sem_alterar_senha_ou_resposta_publica()
    {
        var user = await Seed(); Mail.FailDelivery = true;
        using var response = await Request(user.Email); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Until(async () => { using var scope = host.App.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserAuditEvents.AnyAsync(x => x.Action == "PasswordResetDeliveryFailed"); });
        using var scope = host.App.Services.CreateScope(); var token = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PasswordResetTokens.SingleAsync();
        Assert.False(token.Delivered); Assert.NotNull(token.InvalidatedAt); Assert.Empty(Mail.Messages); Assert.Equal(Hash, (await Stored(user.Id)).PasswordHash);
    }
    [Theory]
    [InlineData(UserRole.OperationManager)] [InlineData(UserRole.CentralOperator)] [InlineData(UserRole.MaintenanceTechnician)] [InlineData(UserRole.Agent)] [InlineData(null)]
    public async Task Reset_administrativo_exige_Admin(UserRole? role)
    {
        var target = await Seed(); var actor = role.HasValue ? await Seed(role.Value) : null;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/password-recovery/users/{target.Id}");
        if (actor is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(actor.Role, actor.Id));
        using var response = await host.Client.SendAsync(request); Assert.Equal(actor is null ? 401 : 403, (int)response.StatusCode); Assert.Empty(Mail.Messages);
    }
    [Fact]
    public async Task Admin_so_envia_ao_email_cadastrado_e_nao_recebe_segredo()
    {
        var target = await Seed(); var admin = await Seed(UserRole.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/password-recovery/users/{target.Id}") { Content = JsonContent.Create(new { email = "atacante@sentra.invalid", redirectUrl = "https://evil.invalid" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(admin.Role, admin.Id));
        using var response = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Until(() => Task.FromResult(Mail.Messages.Length == 1)); var message = Mail.Messages.Single(); Assert.Equal(target.Email, message.Recipient);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(message.Secret, body); Assert.DoesNotContain("https://", body);
        Assert.Equal(Hash, (await Stored(target.Id)).PasswordHash);
        using var scope = host.App.Services.CreateScope(); var entry = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserAuditEvents.SingleAsync(x => x.Action == "PasswordResetRequested");
        Assert.Equal(admin.Id, entry.ActorUserId); Assert.Equal(target.Id, entry.UserId);
    }
    [Fact]
    public async Task Admin_nao_inicia_recuperacao_de_conta_inativa()
    {
        var target = await Seed(active: false); var admin = await Seed(UserRole.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/password-recovery/users/{target.Id}"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(admin.Role, admin.Id));
        using var response = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(Mail.Messages);
    }
    [Fact]
    public async Task Conta_limitada_mesmo_com_origens_distintas_sem_IP_ou_email_no_banco_de_limites()
    {
        var user = await Seed();
        for (var i = 0; i < 5; i++)
        {
            using var scope = host.App.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().RequestAsync(new(user.Email), "test-origin-" + i, default);
        }
        await Issue(await Seed()); // FIFO barrier.
        Assert.Equal(3, Mail.Messages.Count(m => m.Recipient == user.Email));
        using (var deliveryScope = host.App.Services.CreateScope())
            await deliveryScope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().DeliverAsync(new(user.Email.ToUpperInvariant()), default);
        Assert.Equal(3, Mail.Messages.Count(m => m.Recipient == user.Email));
        // A long-idle key can be pruned and reused in the same transaction; its first attempt still counts.
        clock.Advance(TimeSpan.FromDays(2));
        for (var i = 0; i < 5; i++)
        {
            using var scope = host.App.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().RequestAsync(new(user.Email), "test-origin-" + i, default);
        }
        await Issue(await Seed());
        Assert.Equal(6, Mail.Messages.Count(m => m.Recipient == user.Email));
        using var check = host.App.Services.CreateScope(); var limits = await check.ServiceProvider.GetRequiredService<AppDbContext>().PasswordRecoveryLimits.ToArrayAsync();
        Assert.All(limits, l => Assert.Matches("^[A-F0-9]{64}$", l.Key));
    }
    [Fact]
    public async Task Origem_limita_solicitacoes_e_consumo_sem_confiar_em_X_Forwarded_For()
    {
        var user = await Seed(); var secret = await Issue(user);
        for (var i = 0; i < 20; i++) { using var invalid = await Confirm(new string('B', 43)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password-recovery/confirm") { Content = JsonContent.Create(new { token = secret, newPassword = NewPassword, confirmPassword = NewPassword }) };
        request.Headers.Add("X-Forwarded-For", "203.0.113.11");
        using var limited = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        for (var i = 0; i < 22; i++) { using var generic = await Request($"unknown{i}@sentra.invalid"); Assert.Equal(HttpStatusCode.Accepted, generic.StatusCode); }
        var next = await Seed(); using var dropped = await Request(next.Email); await Task.Delay(100);
        Assert.DoesNotContain(Mail.Messages, x => x.Recipient == next.Email);
        clock.Advance(TimeSpan.FromMinutes(15)); var fresh = await Issue(next); using var success = await Confirm(fresh); Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
    }
    [Fact]
    public async Task Feature_desabilitada_expoe_disponibilidade_sem_provedor_falso()
    {
        await using var disabled = await EquipmentTestHost.StartAsync();
        Assert.False((await disabled.Client.GetFromJsonAsync<JsonElement>("/api/auth/password-recovery")).GetProperty("enabled").GetBoolean());
        using var response = await disabled.Client.PostAsJsonAsync("/api/auth/password-recovery/request", new { email = "unknown@sentra.invalid" }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory]
    [InlineData("Production")] [InlineData("Staging")] [InlineData("Demo")]
    public void Transporte_local_recusado_fora_de_Development_Testing(string environment)
    {
        Assert.Throws<InvalidOperationException>(() => new LocalTestPasswordRecoverySender(environment));
        Assert.False(new PasswordRecoverySettings { Transport = "LocalTest" }.IsSafe(environment));
    }
    [Fact]
    public async Task Transporte_Resend_usou_endpoint_fixo_HTTPS_sem_redirecionamento_ou_senha()
    {
        var secret = new string('C', 43); var handler = new InspectHandler(); using var client = new HttpClient(handler);
        var options = Options.Create(new PasswordRecoverySettings { Enabled = true, From = "acesso@sentra.uno", ApiKey = "test-provider-key-123456789", RateLimitKey = new string('D', 32) });
        var sender = new ResendPasswordRecoverySender(client, options); await sender.SendAsync("sintetico@sentra.invalid", secret, default);
        Assert.Equal("https://api.resend.com/emails", handler.Url); Assert.Equal("Bearer test-provider-key-123456789", handler.Authorization);
        Assert.Contains(PasswordRecoverySettings.ResetUrl + "?token=" + secret, handler.Body); Assert.DoesNotContain(OldPassword, handler.Body);
        handler.Fail = true; var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("sintetico@sentra.invalid", secret, default));
        Assert.DoesNotContain(secret, ex.ToString()); Assert.DoesNotContain("provider-private-body", ex.ToString());
        Assert.True(options.Value.IsSafe("Production"));
        options.Value.RateLimitKey = "weak"; Assert.False(options.Value.IsSafe("Production"));
    }
    private sealed class InspectHandler : HttpMessageHandler
    {
        public string? Url, Authorization, Body; public bool Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri?.ToString(); Authorization = request.Headers.Authorization?.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(Fail ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = new StringContent("provider-private-body") };
        }
    }
    public sealed class RecoveryClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
