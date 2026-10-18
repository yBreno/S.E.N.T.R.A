using Api.Security;
using Application.DTOs.Agent;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsController(IAgentService service) : ControllerBase
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

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
