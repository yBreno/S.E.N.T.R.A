using Application.Common;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public sealed class UserAdministrationService(IUserRepository users, IUserAuditRepository history, IAgentRepository agents,
    IUnitOfWork uow, IUserAdministrationCoordinator coordinator, IPasswordHasher hasher,
    IValidator<CreateUserDto> createValidator, IValidator<UserSearchDto> searchValidator,
    IValidator<UpdateUserProfileDto> profileValidator, IValidator<ChangeUserRoleDto> roleValidator,
    IValidator<ChangeUserStatusDto> statusValidator, IValidator<ChangeOwnPasswordDto> passwordValidator) : IUserAdministrationService
{
    private static AccountDto Map(User u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.Revision, DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc));
    private async Task<User> Load(Guid id, CancellationToken ct) => await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("Conta não encontrada.");
    private async Task<User> Actor(AccountActor actor, bool admin, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(actor.Id, ct);
        if (user is null || !user.IsActive || user.AuthVersion != actor.AuthVersion) throw new UnauthorizedException("Sessão desatualizada. Entre novamente.");
        if (admin && user.Role != UserRole.Admin) throw new ForbiddenException("Somente Admin pode administrar contas.");
        return user;
    }
    private static void Revision(User user, long expected)
    {
        if (user.Revision != expected) throw new ConflictException("A conta mudou. Atualize os detalhes antes de salvar.");
    }
    private async Task PreserveAdmin(User user, bool removingAdmin, CancellationToken ct)
    {
        if (removingAdmin && user.IsActive && user.Role == UserRole.Admin && await users.CountActiveAdminsAsync(ct) <= 1)
            throw new BusinessRuleException("O último Admin ativo não pode ser desativado ou rebaixado.");
    }
    private async Task Audit(Guid userId, Guid actorId, string action, UserRole? before, UserRole? after, CancellationToken ct) =>
        await history.AddAsync(new() { UserId = userId, ActorUserId = actorId, Action = action, PreviousRole = before, NewRole = after }, ct);
    private static AccountChangeDto Result(User user, AccountActor actor) => new(Map(user), user.Id == actor.Id && user.AuthVersion != actor.AuthVersion);

    public async Task<PageDto<AccountDto>> SearchAsync(UserSearchDto query, CancellationToken ct = default)
    {
        await searchValidator.ValidateAndThrowAsync(query, ct);
        var page = await users.SearchAsync(query.Search, query.Role, query.IsActive, query.Page, query.PageSize, ct);
        return new(page.Items.Select(Map).ToList(), page.Total, query.Page, query.PageSize);
    }
    public async Task<AccountDetailsDto> GetAsync(Guid id, CancellationToken ct = default) =>
        new(Map(await Load(id, ct)), (await agents.GetByUserIdAsync(id, ct))?.Id);
    public async Task<PageDto<AccountAuditDto>> HistoryAsync(Guid id, int page, int pageSize, CancellationToken ct = default)
    {
        await searchValidator.ValidateAndThrowAsync(new() { Page = page, PageSize = pageSize }, ct); await Load(id, ct);
        var result = await history.SearchAsync(id, page, pageSize, ct);
        return new(result.Items.Select(e => new AccountAuditDto(e.Id, DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc), e.Action, e.ActorUserId, e.PreviousRole, e.NewRole)).ToList(), result.Total, page, pageSize);
    }
    public Task<AccountChangeDto> CreateAsync(CreateUserDto dto, AccountActor actor, CancellationToken ct = default) => coordinator.ExecuteAsync(async () =>
    {
        await Actor(actor, true, ct); await createValidator.ValidateAndThrowAsync(dto, ct);
        if (await users.EmailExistsAsync(dto.Email, ct)) throw new ConflictException("E-mail já cadastrado.");
        if (await users.CpfExistsAsync(Documents.Digits(dto.Cpf), ct)) throw new ConflictException("Documento já cadastrado.");
        var user = User.Create(dto.Name, dto.Email, dto.Cpf, hasher.Hash(dto.Password), dto.Role!.Value);
        await users.AddAsync(user, ct); await Audit(user.Id, actor.Id, "Created", null, user.Role, ct); await uow.SaveChangesAsync(ct);
        return Result(user, actor);
    }, ct);
    public Task<AccountChangeDto> UpdateAsync(Guid id, UpdateUserProfileDto dto, AccountActor actor, CancellationToken ct = default) => coordinator.ExecuteAsync(async () =>
    {
        await Actor(actor, true, ct); await profileValidator.ValidateAndThrowAsync(dto, ct); var user = await Load(id, ct); Revision(user, dto.Revision);
        if (await users.EmailInUseAsync(dto.Email, id, ct)) throw new ConflictException("E-mail já cadastrado.");
        var before = user.Revision; user.UpdateProfile(dto.Name, dto.Email);
        if (before != user.Revision) { await Audit(id, actor.Id, "ProfileUpdated", null, null, ct); await uow.SaveChangesAsync(ct); }
        return Result(user, actor);
    }, ct);
    public Task<AccountChangeDto> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, AccountActor actor, CancellationToken ct = default) => coordinator.ExecuteAsync(async () =>
    {
        await Actor(actor, true, ct); await roleValidator.ValidateAndThrowAsync(dto, ct); var user = await Load(id, ct); Revision(user, dto.Revision);
        await PreserveAdmin(user, dto.Role != UserRole.Admin, ct);
        var previous = user.Role; user.ChangeRole(dto.Role!.Value);
        if (previous != user.Role) { await Audit(id, actor.Id, "RoleChanged", previous, user.Role, ct); await uow.SaveChangesAsync(ct); }
        return Result(user, actor);
    }, ct);
    public Task<AccountChangeDto> SetActiveAsync(Guid id, ChangeUserStatusDto dto, AccountActor actor, CancellationToken ct = default) => coordinator.ExecuteAsync(async () =>
    {
        await Actor(actor, true, ct); await statusValidator.ValidateAndThrowAsync(dto, ct); var user = await Load(id, ct); Revision(user, dto.Revision);
        await PreserveAdmin(user, dto.IsActive == false, ct);
        var previous = user.IsActive; user.SetActive(dto.IsActive!.Value);
        if (previous != user.IsActive) { await Audit(id, actor.Id, user.IsActive ? "Reactivated" : "Deactivated", null, null, ct); await uow.SaveChangesAsync(ct); }
        return Result(user, actor);
    }, ct);
    public Task ChangeOwnPasswordAsync(ChangeOwnPasswordDto dto, AccountActor actor, CancellationToken ct = default) => coordinator.ExecuteAsync(async () =>
    {
        var user = await Actor(actor, false, ct); await passwordValidator.ValidateAndThrowAsync(dto, ct);
        if (!hasher.Verify(dto.CurrentPassword, user.PasswordHash)) throw new UnauthorizedException("Credenciais inválidas.");
        user.ChangePasswordHash(hasher.Hash(dto.NewPassword));
        await Audit(user.Id, actor.Id, "PasswordChanged", null, null, ct); await uow.SaveChangesAsync(ct); return true;
    }, ct);
}
