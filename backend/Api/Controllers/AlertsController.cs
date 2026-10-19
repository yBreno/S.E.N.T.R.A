using Api.Security;
using Application.DTOs.Alert;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize(Roles = Roles.Staff)]
public class AlertsController(IAlertService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponseDto>>> GetAll([FromQuery] AlertStatus? status, [FromQuery] AlertType? type, CancellationToken ct) =>
        Ok(await service.GetAllAsync(status, type, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<AlertResponseDto>> Resolve(Guid id, CancellationToken ct) => Ok(await service.ResolveAsync(id, ct));
}
