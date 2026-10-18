using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Registro aberto apenas para o papel Agent; papeis elevados exigem token de Admin.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserResponseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var user = await auth.RegisterAsync(dto, User.IsInRole(Roles.Admin), ct);
        return Created($"/api/users/{user.Id}", user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto, CancellationToken ct) =>
        Ok(await auth.LoginAsync(dto, ct));
}
