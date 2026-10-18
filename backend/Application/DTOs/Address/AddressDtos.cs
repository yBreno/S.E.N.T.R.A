using Domain.Enums;
namespace Application.DTOs.Address;

public abstract record AddressWriteDto
{
    public string Street { get; init; } = string.Empty;
    public string? Number { get; init; }
    public string? Complement { get; init; }
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public States State { get; init; }
    public string ZipCode { get; init; } = string.Empty;
    public string? LocationReference { get; init; }
    public string Country { get; init; } = "Brasil";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
public record CreateAddressDto : AddressWriteDto;
public record UpdateAddressDto : AddressWriteDto;

public record AddressResponseDto(Guid Id, string Street, string? Number, string? Complement, string Neighborhood, string City,
    States State, string ZipCode, string? LocationReference, string Country, double Latitude, double Longitude);
