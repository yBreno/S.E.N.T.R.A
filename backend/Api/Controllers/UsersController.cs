using Api.Security;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(IUserAdministrationService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageDto<AccountDto>>> Search([FromQuery] UserSearchDto query, CancellationToken ct) => Ok(await users.SearchAsync(query, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDetailsDto>> Get(Guid id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PageDto<AccountAuditDto>>> History(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await users.HistoryAsync(id, page, pageSize, ct));
    [HttpPost]
    public async Task<ActionResult<AccountChangeDto>> Create(CreateUserDto dto, CancellationToken ct)
    {
        var result = await users.CreateAsync(dto, User.GetAccountActor(), ct); return CreatedAtAction(nameof(Get), new { id = result.Account.Id }, result);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountChangeDto>> Update(Guid id, UpdateUserProfileDto dto, CancellationToken ct) => Ok(await users.UpdateAsync(id, dto, User.GetAccountActor(), ct));
    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<AccountChangeDto>> ChangeRole(Guid id, ChangeUserRoleDto dto, CancellationToken ct) => Ok(await users.ChangeRoleAsync(id, dto, User.GetAccountActor(), ct));
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<AccountChangeDto>> SetActive(Guid id, ChangeUserStatusDto dto, CancellationToken ct) => Ok(await users.SetActiveAsync(id, dto, User.GetAccountActor(), ct));
}
