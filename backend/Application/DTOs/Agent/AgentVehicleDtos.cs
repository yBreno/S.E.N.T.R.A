using Application.DTOs.Equipment;

namespace Application.DTOs.Agent;

public record AgentVehicleDto(Guid AgentId, EquipmentResponseDto? Vehicle);
public record AssignAgentVehicleDto(Guid? VehicleId, Guid? ExpectedVehicleId);
