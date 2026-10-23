using Application.DTOs.Agent;

namespace Application.Interfaces.IServices;

public interface IAgentVehicleService
{
    Task<AgentVehicleDto> GetAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentVehicleDto> AssignAsync(Guid agentId, AssignAgentVehicleDto dto, CancellationToken ct = default);
}
