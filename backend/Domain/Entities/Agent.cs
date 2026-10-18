using Domain.Entities;

namespace WebApplication1.backend.Domain.Entities
{
    public class Agent
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string BadgeNumber { get; set; } // Número da matrícula/distintivo
        public AgentRole Role { get; set; }
    }
}
