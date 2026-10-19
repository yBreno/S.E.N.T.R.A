using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class AgentPositionLogRepository(AppDbContext context) : Repository<AgentPositionLog>(context), IAgentPositionLogRepository
{
    public Task<List<AgentPositionLog>> GetByAgentAsync(Guid agentId, DateTime? from, DateTime? to, int take, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().Where(p => p.AgentId == agentId);
        if (from.HasValue) q = q.Where(p => p.RecordedAt >= from.Value);
        if (to.HasValue) q = q.Where(p => p.RecordedAt <= to.Value);
        return q.OrderByDescending(p => p.RecordedAt).Take(take).ToListAsync(ct);
    }
}
