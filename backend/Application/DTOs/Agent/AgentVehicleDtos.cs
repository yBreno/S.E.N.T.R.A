using Application.DTOs.Equipment;

namespace Application.DTOs.Agent;

public record AgentVehicleDto(Guid AgentId, EquipmentResponseDto? Vehicle);
/// <summary>VehicleId null libera o vinculo. ExpectedVehicleId deve refletir a consulta anterior.</summary>
public record AssignAgentVehicleDto(Guid? VehicleId, Guid? ExpectedVehicleId);
