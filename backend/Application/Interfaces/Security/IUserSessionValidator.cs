namespace Application.Interfaces.Security;

/// <summary>Confronta a identidade assinada com o usuário atual sem alterar a sessão ou o banco.</summary>
public interface IUserSessionValidator
{
    Task<bool> IsCurrentAsync(Guid userId, string role, long authVersion, CancellationToken ct = default);
}
