using Domain.Enums;
namespace Application.DTOs.Agent;

public abstract record AgentWriteDto
{
    public string Name { get; init; } = string.Empty;
    public string BadgeNumber { get; init; } = string.Empty;
    public AgentRole Role { get; init; }
    public Guid? UserId { get; init; }
    public AgentAvailability Availability { get; init; } = AgentAvailability.Offline;
    public string Neighborhood { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
public record CreateAgentDto : AgentWriteDto
{
    public string Cpf { get; init; } = string.Empty;
}
public record UpdateAgentDto : AgentWriteDto;

public record AgentResponseDto(Guid Id, string Name, string MaskedCpf, string BadgeNumber, AgentRole Role,
    AgentAvailability Availability, Guid? UserId,
    string Neighborhood, double Latitude, double Longitude, DateTime LocationUpdatedAt);
