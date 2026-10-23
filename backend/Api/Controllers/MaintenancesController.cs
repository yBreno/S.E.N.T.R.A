using Api.Security;
using Application.DTOs.Maintenance;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/maintenances")]
[Authorize(Roles = Roles.Maintenance)]
public class MaintenancesController(IMaintenanceService service) : ControllerBase
{
    [HttpPost("schedule")]
    public async Task<ActionResult<MaintenanceResponseDto>> Schedule([FromBody] ScheduleMaintenanceDto dto, CancellationToken ct)
    {
        var created = await service.ScheduleAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<MaintenanceResponseDto>> Start(Guid id, CancellationToken ct) => Ok(await service.StartAsync(id, ct));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceResponseDto>>> GetAll([FromQuery] MaintenanceStatus? status, [FromQuery] Guid? equipmentId, CancellationToken ct) =>
        Ok(await service.GetAllAsync(status, equipmentId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    /// <summary>Abre manutencao: equipamento (Malfunctioning) vai para UnderMaintenance.</summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceResponseDto>> Open([FromBody] OpenMaintenanceDto dto, CancellationToken ct)
    {
        var created = await service.OpenAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Fecha manutencao: equipamento volta a Operational e o alerta e resolvido.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<MaintenanceResponseDto>> Close(Guid id, [FromBody] CloseMaintenanceDto dto, CancellationToken ct) =>
        Ok(await service.CloseAsync(id, dto, ct));
}
