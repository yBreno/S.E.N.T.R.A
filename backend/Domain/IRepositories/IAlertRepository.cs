using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;
public interface IAlertRepository : IRepository<Alert>
{
    Task<List<Alert>> GetByStatusAsync(AlertStatus? status, CancellationToken ct = default);
    Task<List<Alert>> GetOpenByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
}
