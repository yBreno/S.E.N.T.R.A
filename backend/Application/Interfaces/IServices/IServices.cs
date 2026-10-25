using Application.DTOs.Address;
using Application.DTOs.Agent;
using Application.DTOs.Alert;
using Application.DTOs.Auth;
using Application.DTOs.Dashboard;
using Application.DTOs.Equipment;
using Application.DTOs.Maintenance;
using Domain.Enums;
namespace Application.Interfaces.IServices;

public interface IAddressService : IService<AddressResponseDto, CreateAddressDto, UpdateAddressDto>;

public interface IAgentService : IService<AgentResponseDto, CreateAgentDto, UpdateAgentDto>
{
    Task<AgentResponseDto> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}

public interface IEquipmentService : IService<EquipmentResponseDto, CreateEquipmentDto, UpdateEquipmentDto>
{
    Task<IReadOnlyList<EquipmentResponseDto>> SearchAsync(EquipmentSearchDto filter, CancellationToken ct = default);
    Task<EquipmentResponseDto> ChangeStatusAsync(Guid id, ChangeEquipmentStatusDto dto, bool isManager, CancellationToken ct = default);
}

public interface IMaintenanceService
{
    Task<MaintenanceResponseDto> ScheduleAsync(ScheduleMaintenanceDto dto, CancellationToken ct = default);
    Task<MaintenanceResponseDto> StartAsync(Guid id, CancellationToken ct = default);
    Task PrepareRecommendationAsync(Guid equipmentId, Guid recommendationId, DateTime date, string description, CancellationToken ct = default);
    Task<MaintenanceResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceResponseDto>> GetAllAsync(MaintenanceStatus? status, Guid? equipmentId, CancellationToken ct = default);
    Task<MaintenanceResponseDto> OpenAsync(OpenMaintenanceDto dto, CancellationToken ct = default);
    Task<MaintenanceResponseDto> CloseAsync(Guid id, CloseMaintenanceDto dto, CancellationToken ct = default);
}

public interface IAlertService
{
    Task<AlertResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AlertResponseDto>> GetAllAsync(AlertStatus? status, AlertType? type, CancellationToken ct = default);
    Task<AlertResponseDto> ResolveAsync(Guid id, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default, bool includePredictive = true);
}

public interface IAuthService
{
    Task<UserResponseDto> RegisterAsync(RegisterDto dto, bool callerIsAdmin, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
}
