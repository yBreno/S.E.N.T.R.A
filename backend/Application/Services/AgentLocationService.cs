using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class AgentLocationService(IAgentRepository agents, IAgentPositionLogRepository positions, IUnitOfWork uow,
    IValidator<UpdateAgentLocationDto> validator, TimeProvider clock) : IAgentLocationService
{
    public async Task<AgentPositionDto> UpdateLocationAsync(Guid agentId, UpdateAgentLocationDto dto, Guid? callerUserId, bool isStaff, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var agent = await agents.GetByIdAsync(agentId, ct) ?? throw new NotFoundException("Agente nao encontrado.");
        if (!isStaff && (callerUserId is null || agent.UserId != callerUserId))
            throw new ForbiddenException("O agente so pode atualizar a propria localizacao.");

        var now = clock.GetUtcNow().UtcDateTime;
        agent.Neighborhood = dto.Neighborhood.Trim();
        agent.Latitude = dto.Latitude;
        agent.Longitude = dto.Longitude;
        agent.LocationUpdatedAt = now;
        if (dto.Availability.HasValue) agent.Availability = dto.Availability.Value;

        var log = new AgentPositionLog
        {
            AgentId = agent.Id,
            RecordedAt = now,
            Neighborhood = agent.Neighborhood,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };
        await positions.AddAsync(log, ct);
        await uow.SaveChangesAsync(ct);
        return log.ToResponse();
    }

    public async Task<IReadOnlyList<AgentPositionDto>> GetHistoryAsync(Guid agentId, DateTime? from, DateTime? to, int take, CancellationToken ct = default)
    {
        if (!await agents.ExistsAsync(agentId, ct)) throw new NotFoundException("Agente nao encontrado.");
        return (await positions.GetByAgentAsync(agentId, from, to, Math.Clamp(take, 1, 1000), ct)).Select(p => p.ToResponse()).ToList();
    }
}
