using Application.DTOs.Auth;
namespace Application.Interfaces.IServices;

public interface IUserAdministrationService
{
    Task<PageDto<AccountDto>> SearchAsync(UserSearchDto query, CancellationToken ct = default);
    Task<AccountDetailsDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PageDto<AccountAuditDto>> HistoryAsync(Guid id, int page, int pageSize, CancellationToken ct = default);
    Task<AccountChangeDto> CreateAsync(CreateUserDto dto, AccountActor actor, CancellationToken ct = default);
    Task<AccountChangeDto> UpdateAsync(Guid id, UpdateUserProfileDto dto, AccountActor actor, CancellationToken ct = default);
    Task<AccountChangeDto> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, AccountActor actor, CancellationToken ct = default);
    Task<AccountChangeDto> SetActiveAsync(Guid id, ChangeUserStatusDto dto, AccountActor actor, CancellationToken ct = default);
    Task ChangeOwnPasswordAsync(ChangeOwnPasswordDto dto, AccountActor actor, CancellationToken ct = default);
}
