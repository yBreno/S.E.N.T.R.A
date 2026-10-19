using Domain.Enums;
namespace Domain.Entities;

public class Agent : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public AgentAvailability Availability { get; set; } = AgentAvailability.Offline;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // Localizacao atual (RD-01: bairro + lat/long obrigatorios)
    public string Neighborhood { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime LocationUpdatedAt { get; set; } = DateTime.UtcNow;
}
