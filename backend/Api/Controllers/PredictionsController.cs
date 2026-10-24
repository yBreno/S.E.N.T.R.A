using Api.Security;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/predictions")]
[Authorize(Roles = Roles.Staff)]
public class PredictionsController(
    IAnomalyService anomalies,
    IFailurePredictionService failures,
    IPredictiveAnalysisService analysis, IEquipmentRiskService equipmentRisk) : ControllerBase
{
    /// <summary>Índice heurístico por equipamento (0–100), fatores e suficiência do histórico; não é probabilidade.</summary>
    [HttpGet("equipment-risk")]
    public async Task<ActionResult<IReadOnlyList<EquipmentRiskDto>>> GetEquipmentRisk(CancellationToken ct) =>
        Ok(await equipmentRisk.GetAllAsync(ct));

    /// <summary>Deteccao de anomalias (picos atipicos por Z-Score e falhas repetidas em um mesmo ponto).</summary>
    [HttpGet("anomalies")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<IReadOnlyList<AnomalyDto>>> GetAnomalies(CancellationToken ct) =>
        Ok(await anomalies.DetectAsync(ct));

    /// <summary>Tendencias de disponibilidade e previsao de indisponibilidade futura via MTBF.</summary>
    [HttpGet("failures")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<FailurePredictionDto>>> GetFailurePredictions(
        [FromQuery] bool onlyAtRisk = false, CancellationToken ct = default) =>
        Ok(await failures.PredictAsync(onlyAtRisk, ct));

    /// <summary>Executa varredura preditiva completa (risco, anomalias, MTBF) e gera alertas preventivos e recomendacoes.</summary>
    [HttpPost("analyze")]
    [Authorize(Roles = Roles.Monitoring)]
    public async Task<ActionResult<PredictiveAnalysisResultDto>> Analyze(CancellationToken ct) =>
        Ok(await analysis.RunAsync(ct));
}
