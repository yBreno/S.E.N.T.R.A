using Domain.Enums;
namespace Application.DTOs.Auth;

public record CreateUserDto : RegisterDto;
public record UpdateUserProfileDto(string Name, string Email, long Revision);
public record ChangeUserRoleDto(UserRole? Role, long Revision);
public record ChangeUserStatusDto(bool? IsActive, long Revision);
public record ChangeOwnPasswordDto(string CurrentPassword, string NewPassword);
public record UserSearchDto
{
    public string? Search { get; init; }
    public UserRole? Role { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
public record AccountDto(Guid Id, string Name, string Email, UserRole Role, bool IsActive, long Revision, DateTime CreatedAt);
public record AccountDetailsDto(AccountDto Account, Guid? AgentId);
public record AccountChangeDto(AccountDto Account, bool Reauthenticate);
public record AccountAuditDto(Guid Id, DateTime CreatedAt, string Action, Guid? ActorUserId, UserRole? PreviousRole, UserRole? NewRole);
public record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record AccountActor(Guid Id, long AuthVersion);
