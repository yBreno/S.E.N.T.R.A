using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/telemetry")]
[Authorize(Roles = Roles.Staff)]
public class TelemetryController(ITelemetryService service) : ControllerBase
{
    /// <summary>Registra um log de telemetria de equipamento e aciona alertas/status caso o evento seja critico.</summary>
    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> Register([FromBody] CreateTelemetryDto dto, CancellationToken ct) =>
        Ok(await service.RegisterAsync(dto, ct));

    [HttpGet("equipment/{equipmentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TelemetryResponseDto>>> GetByEquipment(
        Guid equipmentId, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await service.GetByEquipmentAsync(equipmentId, take, ct));
}
