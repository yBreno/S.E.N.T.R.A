using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/risk")]
[Authorize(Roles = Roles.Monitoring)]
public class RiskController(IRiskService service) : ControllerBase
{
    /// <summary>Indice de risco transparente (0..100) de todos os bairros, com fatores e formula auditavel.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NeighborhoodRiskDto>>> GetAll([FromQuery] DateTime? at, CancellationToken ct) =>
        Ok(await service.GetAllAsync(at, ct));

    /// <summary>Indice de risco detalhado de um bairro especifico.</summary>
    [HttpGet("{neighborhood}")]
    public async Task<ActionResult<NeighborhoodRiskDto>> GetByNeighborhood(string neighborhood, [FromQuery] DateTime? at, CancellationToken ct) =>
        Ok(await service.GetByNeighborhoodAsync(neighborhood, at, ct));
}
