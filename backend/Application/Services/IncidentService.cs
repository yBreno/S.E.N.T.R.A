using Application.DTOs.Equipment;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class IncidentService(IIncidentRepository repo, IEquipmentRepository equipments, IEquipmentService equipmentService, IRiskService risk,
    IRecommendationService recommendations, IUnitOfWork uow, IValidator<CreateIncidentDto> validator, TimeProvider clock) : IIncidentService
{
    /// <summary>Fluxo integrado: registra -> atualiza status do equipamento (+alerta) -> reavalia risco -> gera recomendacoes.</summary>
    public async Task<IncidentRegistrationResultDto> RegisterAsync(CreateIncidentDto dto, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(dto, ct);

        Equipment? equipment = null;
        if (dto.EquipmentId is { } eid)
            equipment = await equipments.GetDetailedAsync(eid, ct) ?? throw new NotFoundException("Equipamento nao encontrado.");

        var now = clock.GetUtcNow().UtcDateTime;
        var occurred = dto.OccurredAt ?? now;
        if (occurred > now.AddMinutes(5)) throw new BusinessRuleException("A data da ocorrencia nao pode estar no futuro.");

        var neighborhood = (dto.Neighborhood ?? equipment?.Address?.Neighborhood)?.Trim();
        var lat = dto.Latitude ?? equipment?.Address?.Latitude;
        var lon = dto.Longitude ?? equipment?.Address?.Longitude;
        if (string.IsNullOrWhiteSpace(neighborhood) || lat is null || lon is null)
            throw new BusinessRuleException("RD-01: a ocorrencia precisa de bairro e latitude/longitude validos.");

        var incident = new Incident
        {
            Type = dto.Type,
            Severity = dto.Severity,
            Neighborhood = neighborhood,
            Latitude = lat.Value,
            Longitude = lon.Value,
            OccurredAt = occurred,
            Description = dto.Description?.Trim(),
            EquipmentId = equipment?.Id
        };
        await repo.AddAsync(incident, ct);
        await uow.SaveChangesAsync(ct);

        var statusChanged = false;
        if (equipment is not null && dto.Type == IncidentType.EquipmentFailure && equipment.Status == EquipmentStatus.Operational)
        {
            // RF-05: Malfunctioning gera Alert automatico (reaproveita a regra do EquipmentService).
            await equipmentService.ChangeStatusAsync(equipment.Id,
                new ChangeEquipmentStatusDto(EquipmentStatus.Malfunctioning, $"Ocorrencia {incident.Id}."), true, ct);
            statusChanged = true;
        }

        var riskAfter = await risk.FindByNeighborhoodAsync(neighborhood, null, ct);
        var recs = new List<RecommendationResponseDto>();
        if (riskAfter is not null) recs.AddRange(await recommendations.GenerateForRiskAsync(riskAfter, ct));
        return new IncidentRegistrationResultDto(incident.ToResponse(), statusChanged, riskAfter, recs);
    }

    public async Task<IncidentResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Ocorrencia {id} nao encontrada.")).ToResponse();

    public async Task<IReadOnlyList<IncidentResponseDto>> SearchAsync(DateTime? from, DateTime? to, string? neighborhood, IncidentType? type, CancellationToken ct = default) =>
        (await repo.SearchAsync(new IncidentFilter(from, to, neighborhood, type), ct)).Select(i => i.ToResponse()).ToList();
}
