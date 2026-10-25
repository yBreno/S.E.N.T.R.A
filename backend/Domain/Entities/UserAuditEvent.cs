using Domain.Enums;
namespace Domain.Entities;

/// <summary>Histórico sem senha, token, CPF, nomes, e-mails ou valores de credenciais.</summary>
public class UserAuditEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public UserRole? PreviousRole { get; set; }
    public UserRole? NewRole { get; set; }
}
