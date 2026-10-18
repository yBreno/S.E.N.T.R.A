namespace Domain.Entities;
public class TelemetryLog : BaseEntity
{
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // Ex: "Conexao Perdida", "Bateria Fraca"
    public string? PayloadData { get; set; }              // JSON bruto do sensor
    public double? Latitude { get; set; }                 // Usado a partir do RF-08 (Should)
    public double? Longitude { get; set; }
}
