using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;

public record IncidentFilter(DateTime? From, DateTime? To, string? Neighborhood, IncidentType? Type);

public interface IIncidentRepository : IRepository<Incident>
{
    Task<List<Incident>> SearchAsync(IncidentFilter filter, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}
