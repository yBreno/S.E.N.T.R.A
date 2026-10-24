using Api.Security;
using Application.DTOs.Agent;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsController(IAgentService service, IAgentLocationService locationService, IAgentVehicleService vehicleService) : ControllerBase
{
    /// <summary>Localizacao de agentes (LGPD): apenas Admin, OperationManager e CentralOperator.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<IReadOnlyList<AgentResponseDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    /// <summary>Agente autenticado consulta apenas o proprio perfil.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<AgentResponseDto>> GetMe(CancellationToken ct) =>
        Ok(await service.GetByUserIdAsync(User.GetUserId(), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<AgentResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<AgentResponseDto>> Create([FromBody] CreateAgentDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<AgentResponseDto>> Update(Guid id, [FromBody] UpdateAgentDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    /// <summary>Atualiza a posicao GPS do agente e grava no historico de telemetria. O proprio agente pode atualizar apenas a si mesmo; gestores/monitoramento podem atualizar qualquer agente.</summary>
    [HttpPut("{id:guid}/location")]
    public async Task<ActionResult<AgentPositionDto>> UpdateLocation(Guid id, [FromBody] UpdateAgentLocationDto dto, CancellationToken ct)
    {
        return Ok(await locationService.UpdateLocationAsync(id, dto, User.GetUserId(), User.IsMonitoring(), ct));
    }

    /// <summary>Historico de posicoes GPS do agente (LGPD: apenas Admin, OperationManager e CentralOperator).</summary>
    [HttpGet("{id:guid}/positions")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<IReadOnlyList<AgentPositionDto>>> GetPositions(
        Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await locationService.GetHistoryAsync(id, from, to, take, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/vehicle")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<AgentVehicleDto>> GetVehicle(Guid id, CancellationToken ct) =>
        Ok(await vehicleService.GetAsync(id, ct));

    [HttpPut("{id:guid}/vehicle")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<AgentVehicleDto>> AssignVehicle(Guid id, [FromBody] AssignAgentVehicleDto dto, CancellationToken ct) =>
        Ok(await vehicleService.AssignAsync(id, dto, ct));
}
