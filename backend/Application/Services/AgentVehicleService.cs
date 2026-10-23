using Application.DTOs.Agent;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Domain.IRepositories;

namespace Application.Services;

public class AgentVehicleService(IAgentRepository agents, IEquipmentRepository equipments, IUnitOfWork uow) : IAgentVehicleService
{
    private async Task<Vehicle?> CurrentAsync(Guid agentId, CancellationToken ct)
    {
        if (!await agents.ExistsAsync(agentId, ct)) throw new NotFoundException("Agente nao encontrado.");
        var linked = await equipments.GetVehiclesByAgentAsync(agentId, ct);
        if (linked.Count > 1) throw new ConflictException("O agente possui mais de um veiculo vinculado. Revise os vinculos existentes antes de continuar.");
        return linked.SingleOrDefault();
    }

    public async Task<AgentVehicleDto> GetAsync(Guid agentId, CancellationToken ct = default) =>
        new(agentId, (await CurrentAsync(agentId, ct))?.ToResponse());

    public async Task<AgentVehicleDto> AssignAsync(Guid agentId, AssignAgentVehicleDto dto, CancellationToken ct = default)
    {
        var current = await CurrentAsync(agentId, ct);
        if (current?.Id != dto.ExpectedVehicleId)
            throw new ConflictException("O vinculo do agente mudou. Atualize os detalhes antes de salvar.");

        Vehicle? next = null;
        if (dto.VehicleId is { } id)
        {
            var equipment = await equipments.GetDetailedAsync(id, ct) ?? throw new NotFoundException("Veiculo nao encontrado.");
            next = equipment as Vehicle ?? throw new BusinessRuleException("Somente veiculos, motos e veiculos autonomos podem ser associados a um agente.");
            if (next.CurrentAgentId.HasValue && next.CurrentAgentId != agentId)
                throw new ConflictException("O veiculo ja esta vinculado a outro agente.");
        }

        if (current?.Id == next?.Id) return new(agentId, current?.ToResponse());
        // As duas pontas do vinculo mudam na mesma unidade de trabalho, sem editar atributos do veiculo.
        if (current is not null) current.CurrentAgentId = null;
        if (next is not null) next.CurrentAgentId = agentId;
        await uow.SaveChangesAsync(ct);
        return new(agentId, next?.ToResponse());
    }
}
