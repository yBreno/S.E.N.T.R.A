using Application.DTOs.Maintenance;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class MaintenanceService(IMaintenanceRecordRepository repo, IEquipmentRepository equipments, IAgentRepository agents,
    IAlertRepository alerts, IUnitOfWork uow, IValidator<OpenMaintenanceDto> ov, IValidator<CloseMaintenanceDto> cv,
    IValidator<ScheduleMaintenanceDto> scheduleValidator) : IMaintenanceService
{
    private static void CheckState(Equipment equipment, MaintenanceKind kind)
    {
        var expected = kind == MaintenanceKind.Preventive ? EquipmentStatus.Operational : EquipmentStatus.Malfunctioning;
        if (equipment.Status != expected)
            throw new BusinessRuleException($"Manutencao {kind} exige equipamento {expected}.");
    }

    private async Task EnsureAvailableAsync(Guid equipmentId, CancellationToken ct)
    {
        if ((await repo.GetByEquipmentAsync(equipmentId, ct)).Any(m => m.Status != MaintenanceStatus.Closed))
            throw new ConflictException("Ja existe uma ordem agendada ou em andamento para este equipamento.");
    }

    private async Task<MaintenanceRecord> PlanAsync(ScheduleMaintenanceDto dto, Guid? recommendationId, CancellationToken ct)
    {
        await scheduleValidator.ValidateAndThrowAsync(dto, ct);
        var equipment = await equipments.GetDetailedAsync(dto.EquipmentId, ct) ?? throw new NotFoundException("Equipamento nao encontrado.");
        CheckState(equipment, dto.Kind);
        await EnsureAvailableAsync(equipment.Id, ct);
        if (dto.AgentId is { } agentId && !await agents.ExistsAsync(agentId, ct)) throw new NotFoundException("Agente nao encontrado.");
        var record = new MaintenanceRecord
        {
            EquipmentId = equipment.Id, Equipment = equipment, AgentId = dto.AgentId, ScheduledDate = dto.ScheduledDate,
            Kind = dto.Kind, RecommendationId = recommendationId, Description = dto.Description.Trim(),
            Urgency = dto.Urgency, PeriodicityDays = dto.PeriodicityDays, Status = MaintenanceStatus.Scheduled
        };
        await repo.AddAsync(record, ct);
        equipment.MaintenanceNeed = dto.Kind == MaintenanceKind.Preventive ? MaintenanceNeed.Scheduled : MaintenanceNeed.Immediate;
        equipment.NextMaintenanceDate = dto.ScheduledDate;
        return record;
    }

    public async Task<MaintenanceResponseDto> ScheduleAsync(ScheduleMaintenanceDto dto, CancellationToken ct = default)
    {
        var record = await PlanAsync(dto, null, ct);
        await uow.SaveChangesAsync(ct);
        return record.ToResponse();
    }

    public async Task PrepareRecommendationAsync(Guid equipmentId, Guid recommendationId, DateTime date, string description, CancellationToken ct = default)
    {
        var equipment = await equipments.GetDetailedAsync(equipmentId, ct) ?? throw new NotFoundException("Equipamento nao encontrado.");
        await PlanAsync(new ScheduleMaintenanceDto { EquipmentId = equipmentId, ScheduledDate = date, Description = description,
            Kind = equipment.Status == EquipmentStatus.Malfunctioning ? MaintenanceKind.Corrective : MaintenanceKind.Preventive }, recommendationId, ct);
    }

    public async Task<MaintenanceResponseDto> StartAsync(Guid id, CancellationToken ct = default)
    {
        var record = await LoadAsync(id, ct);
        if (record.Status != MaintenanceStatus.Scheduled) throw new BusinessRuleException("Somente uma ordem agendada pode ser iniciada.");
        // Uma falha real posterior ao agendamento transforma a intervencao em corretiva.
        var kind = record.Equipment.Status == EquipmentStatus.Malfunctioning ? MaintenanceKind.Corrective : record.Kind;
        CheckState(record.Equipment, kind);
        var now = DateTime.UtcNow;
        if (await repo.HasOverlapAsync(record.EquipmentId, now, null, record.Id, ct))
            throw new ConflictException("Existe manutencao sobreposta.");
        record.Equipment.ChangeStatus(EquipmentStatus.UnderMaintenance);
        record.Kind = kind;
        record.InitDate = now;
        record.Status = MaintenanceStatus.Open;
        await uow.SaveChangesAsync(ct);
        return record.ToResponse();
    }
    private async Task<MaintenanceRecord> LoadAsync(Guid id, CancellationToken ct) =>
        await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Manutencao {id} nao encontrada.");

    public async Task<MaintenanceResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) => (await LoadAsync(id, ct)).ToResponse();

    public async Task<IReadOnlyList<MaintenanceResponseDto>> GetAllAsync(MaintenanceStatus? status, Guid? equipmentId, CancellationToken ct = default)
    {
        var list = equipmentId.HasValue ? await repo.GetByEquipmentAsync(equipmentId.Value, ct)
                 : status.HasValue ? await repo.GetByStatusAsync(status.Value, ct)
                 : await repo.GetAllAsync(ct);
        return list.Where(m => !status.HasValue || m.Status == status).Select(m => m.ToResponse()).ToList();
    }

    /// <summary>RF-05: abrir manutencao move o equipamento para UnderMaintenance.</summary>
    public async Task<MaintenanceResponseDto> OpenAsync(OpenMaintenanceDto dto, CancellationToken ct = default)
    {
        await ov.ValidateAndThrowAsync(dto, ct);
        var equipment = await equipments.GetDetailedAsync(dto.EquipmentId, ct) ?? throw new NotFoundException("Equipamento nao encontrado.");
        if (dto.AgentId is { } agentId && !await agents.ExistsAsync(agentId, ct)) throw new NotFoundException("Agente nao encontrado.");

        var init = dto.InitDate ?? DateTime.UtcNow;
        if (init > DateTime.UtcNow || init.Kind != DateTimeKind.Utc)
            throw new BusinessRuleException("Inicio deve ser uma data UTC passada ou atual; use agendamento para uma data futura.");
        await EnsureAvailableAsync(equipment.Id, ct);
        if (await repo.HasOverlapAsync(equipment.Id, init, null, null, ct))
            throw new ConflictException("Ja existe manutencao em andamento/sobreposta para este equipamento.");

        CheckState(equipment, MaintenanceKind.Corrective);
        equipment.ChangeStatus(EquipmentStatus.UnderMaintenance);
        equipment.MaintenanceNeed = MaintenanceNeed.Immediate;
        equipment.NextMaintenanceDate = null;

        var record = new MaintenanceRecord
        {
            EquipmentId = equipment.Id,
            Equipment = equipment,
            AgentId = dto.AgentId,
            InitDate = init,
            Description = dto.Description.Trim(),
            Urgency = dto.Urgency,
            PeriodicityDays = dto.PeriodicityDays,
            Status = MaintenanceStatus.Open
        };
        await repo.AddAsync(record, ct);
        await uow.SaveChangesAsync(ct);
        return record.ToResponse();
    }

    /// <summary>RF-05: fechar volta para Operational, define NextMaintenanceDate pela periodicidade e fecha o alerta.</summary>
    public async Task<MaintenanceResponseDto> CloseAsync(Guid id, CloseMaintenanceDto dto, CancellationToken ct = default)
    {
        await cv.ValidateAndThrowAsync(dto, ct);
        var record = await LoadAsync(id, ct);
        var end = dto.EndDate ?? DateTime.UtcNow;
        if (end > DateTime.UtcNow || end.Kind != DateTimeKind.Utc)
            throw new BusinessRuleException("Conclusao deve ser uma data UTC passada ou atual.");
        if (record.Equipment.Status != EquipmentStatus.UnderMaintenance)
            throw new BusinessRuleException("Equipamento deve estar em manutencao para concluir a ordem.");

        record.Close(end, dto.Cost); // valida EndDate >= InitDate
        var equipment = record.Equipment;
        equipment.ChangeStatus(EquipmentStatus.Operational);
        equipment.NextMaintenanceDate = record.PeriodicityDays is { } days ? end.AddDays(days) : null;
        equipment.MaintenanceNeed = record.PeriodicityDays.HasValue ? MaintenanceNeed.Periodic : MaintenanceNeed.None;

        foreach (var alert in await alerts.GetOpenByEquipmentAsync(equipment.Id, ct)) alert.Resolve();

        await uow.SaveChangesAsync(ct);
        return record.ToResponse();
    }
}
