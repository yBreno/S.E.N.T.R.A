namespace WebApplication1.backend.Domain.Entities
{
    public class Supplier
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Cnpj { get; set; }
        public string? SupportPhone { get; set; }
        public string? SupportEmail { get; set; }
    }
}
