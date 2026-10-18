using Application.DTOs.Address;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class AddressService(IAddressRepository repo, IUnitOfWork uow, IValidator<CreateAddressDto> cv, IValidator<UpdateAddressDto> uv)
    : Service<Domain.Entities.Address, CreateAddressDto, UpdateAddressDto, AddressResponseDto>(repo, uow, cv, uv), IAddressService
{
    protected override string EntityName => "Endereco";
    protected override AddressResponseDto ToResponse(Domain.Entities.Address e) => e.ToResponse();

    protected override Task<Domain.Entities.Address> BuildAsync(CreateAddressDto dto, CancellationToken ct)
    {
        var a = new Domain.Entities.Address();
        a.Apply(dto);
        return Task.FromResult(a);
    }

    protected override Task ApplyUpdateAsync(Domain.Entities.Address e, UpdateAddressDto dto, CancellationToken ct)
    {
        e.Apply(dto);
        return Task.CompletedTask;
    }

    protected override async Task EnsureCanDeleteAsync(Domain.Entities.Address e, CancellationToken ct)
    {
        if (await repo.IsInUseAsync(e.Id, ct)) throw new ConflictException("Endereco em uso por equipamentos/bases e nao pode ser excluido.");
    }
}
