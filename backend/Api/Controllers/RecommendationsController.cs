using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize(Roles = Roles.Staff)]
public class RecommendationsController(IRecommendationService service) : ControllerBase
{
    /// <summary>
    /// Lista recomendacoes preventivas com explicabilidade JSON.
    /// MaintenanceTechnician visualiza apenas recomendacoes de manutencao (SendMaintenance).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecommendationResponseDto>>> GetAll(
        [FromQuery] RecommendationStatus? status, CancellationToken ct)
    {
        var maintenanceOnly = User.IsInRole(Roles.MaintenanceTechnician)
            && !User.IsInRole(Roles.Admin)
            && !User.IsInRole(Roles.OperationManager)
            && !User.IsInRole(Roles.CentralOperator);
        return Ok(await service.GetAllAsync(status, maintenanceOnly, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecommendationResponseDto>> Get(Guid id, CancellationToken ct)
    {
        var maintenanceOnly = User.IsInRole(Roles.MaintenanceTechnician)
            && !User.IsInRole(Roles.Admin)
            && !User.IsInRole(Roles.OperationManager)
            && !User.IsInRole(Roles.CentralOperator);
        return Ok(await service.GetByIdAsync(id, maintenanceOnly, ct));
    }

    /// <summary>Registra aceite de reforço para avaliação operacional; para equipamento, reutiliza o fluxo de manutenção sem duplicar ordem ativa. Não desloca agentes automaticamente.</summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<RecommendationResponseDto>> Accept(Guid id, CancellationToken ct) =>
        Ok(await service.AcceptAsync(id, ct));

    /// <summary>Descarta a recomendacao preventiva.</summary>
    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<RecommendationResponseDto>> Dismiss(Guid id, CancellationToken ct) =>
        Ok(await service.DismissAsync(id, ct));
}
