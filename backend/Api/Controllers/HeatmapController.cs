using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/heatmap")]
[Authorize(Roles = Roles.Monitoring)]
public class HeatmapController(IHeatmapService service) : ControllerBase
{
    /// <summary>
    /// Camada de mapa de calor (Heatmap) com suporte as metricas Incidents, Failures e Risk,
    /// e filtros dinamicos por periodo, faixa horaria, dia da semana, bairro e tipo de ocorrencia.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HeatmapDto>> Get(
        [FromQuery] HeatmapMetric metric = HeatmapMetric.Incidents,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? fromHour = null,
        [FromQuery] int? toHour = null,
        [FromQuery] DayOfWeek? dayOfWeek = null,
        [FromQuery] string? neighborhood = null,
        [FromQuery] IncidentType? type = null,
        CancellationToken ct = default) =>
        Ok(await service.GetAsync(new HeatmapQueryDto
        {
            Metric = metric,
            From = from,
            To = to,
            FromHour = fromHour,
            ToHour = toHour,
            DayOfWeek = dayOfWeek,
            Neighborhood = neighborhood,
            Type = type
        }, ct));
}
