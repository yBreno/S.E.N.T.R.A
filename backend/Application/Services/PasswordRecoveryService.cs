using System.Security.Cryptography;
using System.Text;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Application.Security;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Application.Services;

public sealed class PasswordRecoveryService(IUserRepository users, IPasswordRecoveryRepository tokens,
    IUserAuditRepository audit, IUnitOfWork uow, IUserAdministrationCoordinator coordinator,
    IPasswordRecoveryQueue queue, IPasswordRecoveryEmailSender sender, IPasswordHasher hasher,
    IOptions<PasswordRecoverySettings> settings, TimeProvider clock,
    IValidator<ForgotPasswordDto> requestValidator, IValidator<ResetPasswordDto> resetValidator,
    ILogger<PasswordRecoveryService> logger) : IPasswordRecoveryService
{
    public bool Enabled => settings.Value.Enabled;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    private string LimitKey(string kind, string value) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(settings.Value.RateLimitKey), Encoding.UTF8.GetBytes(kind + ":" + value)));
    private void RequireEnabled() { if (!Enabled) throw new BusinessRuleException("Recuperação por e-mail indisponível neste ambiente."); }

    // This path never looks up Users and never waits for the email provider.
    public async Task RequestAsync(ForgotPasswordDto dto, string origin, CancellationToken ct)
    {
        RequireEnabled(); await requestValidator.ValidateAndThrowAsync(dto, ct);
        try
        {
            var accepted = await coordinator.ExecuteAsync(async () =>
            {
                var allowed = await TakeRequestLimits(Normalize(dto.Email), origin, ct);
                await uow.SaveChangesAsync(ct); return allowed;
            }, ct);
            if (accepted) queue.TryEnqueue(new(Normalize(dto.Email)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { logger.LogWarning("Solicitação de recuperação não pôde ser enfileirada; resposta pública preservada."); }
    }
    private async Task<bool> TakeRequestLimits(string email, string origin, CancellationToken ct)
    {
        await tokens.PruneLimitsAsync(Now, ct);
        // Short circuit prevents an origin at its limit from growing the account-key table.
        return await tokens.TakeLimitAsync(LimitKey("request-origin", origin), 20, Now, ct) &&
            await tokens.TakeLimitAsync(LimitKey("request-account", email), 3, Now, ct);
    }
    public async Task RequestAdministrativeAsync(Guid userId, AccountActor actor, string origin, CancellationToken ct)
    {
        RequireEnabled();
        var request = await coordinator.ExecuteAsync(async () =>
        {
            var administrator = await users.GetByIdAsync(actor.Id, ct);
            if (administrator is null || !administrator.IsActive || administrator.AuthVersion != actor.AuthVersion)
                throw new UnauthorizedException("Sessão desatualizada.");
            if (administrator.Role != UserRole.Admin) throw new ForbiddenException("Somente Admin pode iniciar a redefinição.");
            var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Conta não encontrada.");
            if (!user.IsActive) throw new BusinessRuleException("Reative a conta antes de solicitar uma redefinição.");
            var allowed = await TakeRequestLimits(user.Email, origin, ct);
            if (allowed) await AddAudit(user.Id, actor.Id, "PasswordResetRequested", ct);
            await uow.SaveChangesAsync(ct);
            return allowed ? new PasswordRecoveryRequest(user.Email, user.Id, user.AuthVersion) : null;
        }, ct);
        if (request is null) throw new RateLimitException();
        if (!queue.TryEnqueue(request)) throw new ConflictException("Envio ocupado. Aguarde e tente novamente.");
    }
    public async Task ResetAsync(ResetPasswordDto dto, string origin, CancellationToken ct)
    {
        RequireEnabled();
        var allowed = await coordinator.ExecuteAsync(async () =>
        {
            await tokens.PruneLimitsAsync(Now, ct);
            var result = await tokens.TakeLimitAsync(LimitKey("consume-origin", origin), 20, Now, ct);
            await uow.SaveChangesAsync(ct); return result;
        }, ct);
        if (!allowed) throw new RateLimitException();
        await resetValidator.ValidateAndThrowAsync(dto, ct);
        await coordinator.ExecuteAsync(async () =>
        {
            var token = await tokens.FindTokenAsync(Hash(dto.Token), ct);
            var user = token is null ? null : await users.GetByIdAsync(token.UserId, ct);
            if (token is null || token.InvalidatedAt.HasValue || !token.Delivered || token.ExpiresAt <= Now ||
                user is null || !user.IsActive || user.AuthVersion != token.AuthVersion || !Enum.IsDefined(user.Role))
                throw new BusinessRuleException("Link inválido ou expirado. Solicite uma nova redefinição.");
            user.ChangePasswordHash(hasher.Hash(dto.NewPassword));
            token.InvalidatedAt = Now;
            await AddAudit(user.Id, null, "PasswordResetCompleted", ct);
            await uow.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task DeliverAsync(PasswordRecoveryRequest request, CancellationToken ct)
    {
        if (!Enabled) return;
        var capability = await coordinator.ExecuteAsync(async () =>
        {
            var user = await users.GetByEmailAsync(request.Email, ct);
            if (user is null || !user.IsActive || !Enum.IsDefined(user.Role) ||
                request.UserId.HasValue && (request.UserId != user.Id || request.AuthVersion != user.AuthVersion)) return null;
            // Bind the delivery budget to the actual account too, independent of email collation/aliases.
            if (!await tokens.TakeLimitAsync(LimitKey("delivery-account", user.Id.ToString("N")), 3, Now, ct))
            {
                await uow.SaveChangesAsync(ct); return null;
            }
            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hash = Hash(secret);
            var token = await tokens.ForUserAsync(user.Id, ct);
            if (token is null) { token = new() { UserId = user.Id }; tokens.Add(token); }
            token.TokenHash = hash; token.AuthVersion = user.AuthVersion; token.ExpiresAt = Now.AddMinutes(PasswordRecoverySettings.ExpirationMinutes);
            token.Delivered = false; token.InvalidatedAt = null;
            if (!request.UserId.HasValue) await AddAudit(user.Id, null, "PasswordRecoveryRequested", ct);
            await uow.SaveChangesAsync(ct);
            return new Capability(user.Email, secret, hash);
        }, ct);
        if (capability is null) return;
        var delivered = false;
        try { await sender.SendAsync(capability.Email, capability.Secret, ct); delivered = true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { logger.LogWarning("Entrega de recuperação não confirmada; nenhum dado de destinatário foi registrado."); }
        await coordinator.ExecuteAsync(async () =>
        {
            var token = await tokens.FindTokenAsync(capability.Hash, ct);
            if (token is not null)
            {
                token.Delivered = delivered;
                if (!delivered) { token.InvalidatedAt = Now; await AddAudit(token.UserId, null, "PasswordResetDeliveryFailed", ct); }
                await uow.SaveChangesAsync(ct);
            }
            return true;
        }, ct);
    }
    private async Task AddAudit(Guid userId, Guid? actor, string action, CancellationToken ct) =>
        await audit.AddAsync(new() { UserId = userId, ActorUserId = actor, Action = action, CreatedAt = Now }, ct);
    private sealed class Capability(string email, string secret, string hash)
    {
        public string Email { get; } = email;
        public string Secret { get; } = secret;
        public string Hash { get; } = hash;
        public override string ToString() => nameof(Capability);
    }
}
