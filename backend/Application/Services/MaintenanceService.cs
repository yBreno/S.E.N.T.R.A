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
    IAlertRepository alerts, IUnitOfWork uow, IValidator<OpenMaintenanceDto> ov, IValidator<CloseMaintenanceDto> cv) : IMaintenanceService
{
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
        if (await repo.HasOverlapAsync(equipment.Id, init, null, null, ct))
            throw new ConflictException("Ja existe manutencao em andamento/sobreposta para este equipamento.");

        equipment.ChangeStatus(EquipmentStatus.UnderMaintenance); // exige Malfunctioning (RD-02)

        var record = new MaintenanceRecord
        {
            EquipmentId = equipment.Id,
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
