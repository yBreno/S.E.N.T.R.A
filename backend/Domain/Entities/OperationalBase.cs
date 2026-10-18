using Domain.Entities;

namespace WebApplication1.backend.Domain.Entities
{
    public class OperationalBase
    {
        public Guid Id { get; set; }
        public string Name { get; set; } 
        public string? Description { get; set; }
        public Guid AddressId { get; set; }
        public Address Address { get; set; }
    }
}
