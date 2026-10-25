using Domain.Entities;
using Domain.Enums;
namespace Domain.IRepositories;
public interface IAlertRepository : IRepository<Alert>
{
    Task<List<Alert>> GetByStatusAsync(AlertStatus? status, AlertType? type, CancellationToken ct = default);
    Task<List<Alert>> GetOpenByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
    Task<List<Alert>> GetFailureAlertsAsync(DateTime since, CancellationToken ct = default);
    Task<bool> HasOpenPreventiveAsync(Guid equipmentId, CancellationToken ct = default);
}
