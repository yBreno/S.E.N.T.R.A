using Domain.Enums;
namespace Domain.Entities;

public class UserAuditEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public UserRole? PreviousRole { get; set; }
    public UserRole? NewRole { get; set; }
}
