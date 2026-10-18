namespace Domain.Entities;
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportEmail { get; set; }
}
