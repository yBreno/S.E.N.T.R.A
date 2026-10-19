namespace Domain.Entities;

/// <summary>Historico de posicao de um agente (telemetria de agentes).</summary>
public class AgentPositionLog : BaseEntity
{
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string Neighborhood { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
