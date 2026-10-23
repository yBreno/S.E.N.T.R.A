using Api.Security;
using Application.DTOs.Dashboard;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Monitoring)]
public class DashboardController(IDashboardService service) : ControllerBase
{
    /// <summary>RF-03: total, contagem por status, taxa de disponibilidade e agentes ativos.</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct, [FromQuery] bool includePredictive = true) => Ok(await service.GetAsync(ct, includePredictive));
}
