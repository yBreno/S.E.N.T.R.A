namespace WebApplication1.backend.Domain.Entities
{
    public class MaintenanceRecord
    {
        public Guid Id { get; set; }
        public Guid EquipmentId { get; set; }
        public DateTime InitDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }
        public Guid? AgentId { get; set; }
        public decimal? Cost { get; set; }
    }
}
