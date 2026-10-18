using Domain.Enums;
namespace Domain.Entities;

public class Address : BaseEntity
{
    public string Street { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public States State { get; set; }
    public string ZipCode { get; set; } = string.Empty;
    public string? LocationReference { get; set; }
    public string Country { get; set; } = "Brasil";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
