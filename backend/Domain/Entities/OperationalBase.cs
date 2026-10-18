namespace Domain.Entities;
public class OperationalBase : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AddressId { get; set; }
    public Address Address { get; set; } = null!;
}
