using Api.Security;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/auth/password-recovery")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordRecoveryController(IPasswordRecoveryService recovery) : ControllerBase
{
    private string Origin => HttpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
    [HttpGet, AllowAnonymous]
    public IActionResult Availability() => Ok(new { enabled = recovery.Enabled });
    [HttpPost("request"), AllowAnonymous]
    public async Task<IActionResult> RequestLink(ForgotPasswordDto dto, CancellationToken ct)
    {
        await recovery.RequestAsync(dto, Origin, ct);
        return Accepted(new { message = "Se a conta estiver apta, você receberá um link para redefinir a senha. Aguarde alguns minutos e verifique também o spam." });
    }
    [HttpPost("confirm"), AllowAnonymous]
    public async Task<IActionResult> Confirm(ResetPasswordDto dto, CancellationToken ct)
    {
        await recovery.ResetAsync(dto, Origin, ct); return NoContent();
    }
    [HttpPost("users/{userId:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Administrative(Guid userId, CancellationToken ct)
    {
        await recovery.RequestAdministrativeAsync(userId, User.GetAccountActor(), Origin, ct);
        return Accepted(new { message = "Solicitação registrada. Se a entrega for possível, o titular receberá o link no endereço cadastrado. Nenhuma credencial foi alterada." });
    }
}
