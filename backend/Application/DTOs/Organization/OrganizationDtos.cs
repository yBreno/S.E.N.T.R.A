using Domain.Enums;
namespace Application.DTOs.Organization;

public abstract record OrganizationWriteDto
{
    public string Name { get; init; } = string.Empty;
    public OrganizationType Type { get; init; }
    public string? Description { get; init; }
}
public record CreateOrganizationDto : OrganizationWriteDto;
public record UpdateOrganizationDto : OrganizationWriteDto;
public record OrganizationResponseDto(Guid Id, string Name, OrganizationType Type, string? Description);
