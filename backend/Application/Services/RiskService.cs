using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Domain.Exceptions;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Application.Services;

public class RiskService(IIncidentRepository incidents, IAlertRepository alerts, IEquipmentRepository equipments,
    IOptions<PredictiveSettings> options, TimeProvider clock) : IRiskService
{
    private async Task<List<NeighborhoodRiskDto>> BuildAsync(DateTime? at, CancellationToken ct)
    {
        var s = options.Value;
        var when = at ?? clock.GetUtcNow().UtcDateTime;
        var inc = await incidents.SearchAsync(new IncidentFilter(when.AddDays(-s.DensityWindowDays), when, null, null), ct);
        var failures = await alerts.GetFailureAlertsAsync(when.AddDays(-s.FailureWindowDays), ct);
        var eq = await equipments.GetAllAsync(ct); // com Address
        return RiskCalculator.Calculate(new RiskInput(when, inc, failures, eq), s).OrderByDescending(r => r.Score).ToList();
    }

    public async Task<IReadOnlyList<NeighborhoodRiskDto>> GetAllAsync(DateTime? at = null, CancellationToken ct = default) => await BuildAsync(at, ct);

    public async Task<NeighborhoodRiskDto?> FindByNeighborhoodAsync(string neighborhood, DateTime? at = null, CancellationToken ct = default)
    {
        var n = neighborhood.Trim();
        return (await BuildAsync(at, ct)).FirstOrDefault(r => string.Equals(r.Neighborhood, n, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<NeighborhoodRiskDto> GetByNeighborhoodAsync(string neighborhood, DateTime? at = null, CancellationToken ct = default) =>
        await FindByNeighborhoodAsync(neighborhood, at, ct) ?? throw new NotFoundException($"Sem dados para o bairro '{neighborhood}'.");
}
