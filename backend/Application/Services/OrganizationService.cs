using Application.DTOs.Organization;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class OrganizationService(IOrganizationRepository repo, IUnitOfWork uow, IValidator<CreateOrganizationDto> cv, IValidator<UpdateOrganizationDto> uv)
    : Service<Domain.Entities.Organization, CreateOrganizationDto, UpdateOrganizationDto, OrganizationResponseDto>(repo, uow, cv, uv), IOrganizationService
{
    protected override string EntityName => "Organizacao";
    protected override OrganizationResponseDto ToResponse(Domain.Entities.Organization e) => e.ToResponse();

    protected override async Task<Domain.Entities.Organization> BuildAsync(CreateOrganizationDto dto, CancellationToken ct)
    {
        if (await repo.NameExistsAsync(dto.Name.Trim(), null, ct)) throw new ConflictException("Ja existe uma organizacao com esse nome.");
        var o = new Domain.Entities.Organization();
        o.Apply(dto);
        return o;
    }

    protected override async Task ApplyUpdateAsync(Domain.Entities.Organization e, UpdateOrganizationDto dto, CancellationToken ct)
    {
        if (await repo.NameExistsAsync(dto.Name.Trim(), e.Id, ct)) throw new ConflictException("Ja existe uma organizacao com esse nome.");
        e.Apply(dto);
    }

    protected override async Task EnsureCanDeleteAsync(Domain.Entities.Organization e, CancellationToken ct)
    {
        if (await repo.HasDependentsAsync(e.Id, ct)) throw new ConflictException("Organizacao possui agentes/equipamentos vinculados.");
    }
}
