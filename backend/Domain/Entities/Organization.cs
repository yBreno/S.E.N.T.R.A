using Domain.Enums;
namespace Domain.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public OrganizationType Type { get; set; }
    public string? Description { get; set; }
}
