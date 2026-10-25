using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Application.Common;
namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, IUserAdministrationService accounts, IOptions<PublicRegistrationSettings> registration) : ControllerBase
{
    /// <summary>Registro aberto apenas para o papel Agent; papeis elevados exigem token de Admin.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserResponseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(Roles.Admin))
        {
            var result = await accounts.CreateAsync(new CreateUserDto { Name = dto.Name, Email = dto.Email, Cpf = dto.Cpf, Password = dto.Password, Role = dto.Role ?? Domain.Enums.UserRole.Agent }, User.GetAccountActor(), ct);
            var account = result.Account;
            return Created($"/api/users/{account.Id}", new UserResponseDto(account.Id, account.Name, account.Email, Documents.MaskCpf(dto.Cpf), account.Role));
        }
        if (!registration.Value.AllowAgentSelfRegistration) return StatusCode(403, new { message = "Auto-cadastro desabilitado. Solicite uma conta ao administrador." });
        var user = await auth.RegisterAsync(dto, false, ct); return Created($"/api/users/{user.Id}", user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto, CancellationToken ct) =>
        Ok(await auth.LoginAsync(dto, ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordDto dto, CancellationToken ct)
    {
        await accounts.ChangeOwnPasswordAsync(dto, User.GetAccountActor(), ct); return NoContent();
    }
}
