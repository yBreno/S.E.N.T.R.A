using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;

public interface IRecommendationRepository : IRepository<Recommendation>
{
    Task<List<Recommendation>> GetByFilterAsync(RecommendationStatus? status, RecommendationType? type, CancellationToken ct = default);
    Task<bool> ExistsPendingAsync(RecommendationType type, string neighborhood, Guid? equipmentId, CancellationToken ct = default);
    Task<int> CountPendingAsync(CancellationToken ct = default);
}
