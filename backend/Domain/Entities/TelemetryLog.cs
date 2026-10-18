namespace WebApplication1.backend.Domain.Entities
{
    public class TelemetryLog
    {
        public Guid Id { get; set; }
        public Guid EquipmentId { get; set; }
        public Equipment Equipment { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } // Ex: "Conexão Perdida", "Reinicialização", "Bateria Fraca"
        public string? PayloadData { get; set; } // Dados brutos que vieram do sensor (geralmente um JSON)
    }
}


