using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize(Roles = Roles.Monitoring)]
public class IncidentsController(IIncidentService service) : ControllerBase
{
    /// <summary>Consulta ocorrencias operacionais historicas com filtros por periodo, bairro e tipo.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentResponseDto>>> Search(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? neighborhood,
        [FromQuery] IncidentType? type,
        CancellationToken ct) =>
        Ok(await service.SearchAsync(from, to, neighborhood, type, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentResponseDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetByIdAsync(id, ct));

    /// <summary>
    /// Registra uma ocorrencia e executa o fluxo integrado:
    /// 1) grava a ocorrencia;
    /// 2) atualiza o status do equipamento (se vinculado) e gera alerta;
    /// 3) recalcula o indice de risco do bairro;
    /// 4) gera recomendacoes preventivas acionaveis se o risco for High/Critical.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IncidentRegistrationResultDto>> Register([FromBody] CreateIncidentDto dto, CancellationToken ct)
    {
        var result = await service.RegisterAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Incident.Id }, result);
    }
}
