using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class TelemetryService(ITelemetryLogRepository logs, IEquipmentRepository equipments, IIncidentService incidents, IUnitOfWork uow,
    IValidator<CreateTelemetryDto> validator, TimeProvider clock) : ITelemetryService
{
    private static readonly string[] ConnectionLost = ["Conexão Perdida", "Conexao Perdida", "ConnectionLost", "Conex\u00c3\u00a3o Perdida"];
    private static readonly string[] LowBattery = ["Bateria Fraca", "LowBattery"];

    private static bool Is(string evt, string[] names) => names.Contains(evt.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Eventos criticos atualizam o equipamento: Conexao Perdida -> falha (alerta/risco); Bateria Fraca -> manutencao agendada.</summary>
    public async Task<TelemetryResponseDto> RegisterAsync(CreateTelemetryDto dto, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var e = await equipments.GetDetailedAsync(dto.EquipmentId, ct) ?? throw new NotFoundException("Equipamento nao encontrado.");

        var log = new TelemetryLog
        {
            EquipmentId = e.Id,
            Timestamp = dto.Timestamp ?? clock.GetUtcNow().UtcDateTime,
            EventType = dto.EventType.Trim(),
            PayloadData = dto.PayloadData,
            Latitude = dto.Latitude ?? e.Address?.Latitude,
            Longitude = dto.Longitude ?? e.Address?.Longitude
        };
        await logs.AddAsync(log, ct);

        if (Is(log.EventType, LowBattery) && e.MaintenanceNeed == MaintenanceNeed.None)
            e.MaintenanceNeed = MaintenanceNeed.Scheduled;
        await uow.SaveChangesAsync(ct);

        var triggered = false;
        if (Is(log.EventType, ConnectionLost) && e.Status == EquipmentStatus.Operational)
        {
            await incidents.RegisterAsync(new CreateIncidentDto
            {
                Type = IncidentType.EquipmentFailure,
                Severity = IncidentSeverity.High,
                EquipmentId = e.Id,
                Description = $"Telemetria: {log.EventType}"
            }, ct);
            triggered = true;
        }

        return log.ToResponse(triggered);
    }

    public async Task<IReadOnlyList<TelemetryResponseDto>> GetByEquipmentAsync(Guid equipmentId, int take, CancellationToken ct = default) =>
        (await logs.GetByEquipmentAsync(equipmentId, ct)).Take(Math.Clamp(take, 1, 500))
            .Select(l => l.ToResponse()).ToList();
}
