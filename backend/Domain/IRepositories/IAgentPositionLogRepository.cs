using Domain.Entities;
namespace Domain.IRepositories;

public interface IAgentPositionLogRepository : IRepository<AgentPositionLog>
{
    Task<List<AgentPositionLog>> GetByAgentAsync(Guid agentId, DateTime? from, DateTime? to, int take, CancellationToken ct = default);
}
