using Application.Common;
using Application.DTOs.Address;
using FluentValidation;
namespace Application.Validators;

public abstract class AddressWriteValidator<T> : AbstractValidator<T> where T : AddressWriteDto
{
    protected AddressWriteValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Number).MaximumLength(20);
        RuleFor(x => x.Complement).MaximumLength(100);
        RuleFor(x => x.LocationReference).MaximumLength(200);
        RuleFor(x => x.Neighborhood).NotEmpty().WithMessage("Bairro e obrigatorio (RD-01).").MaximumLength(120);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.State).IsInEnum();
        RuleFor(x => x.ZipCode).Must(Documents.IsValidZipCode).WithMessage("CEP invalido (8 digitos).");
        RuleFor(x => x.Country).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Latitude).Must(Documents.IsValidLatitude).WithMessage("Latitude deve estar entre -90 e 90 (RD-01).");
        RuleFor(x => x.Longitude).Must(Documents.IsValidLongitude).WithMessage("Longitude deve estar entre -180 e 180 (RD-01).");
    }
}
public class CreateAddressDtoValidator : AddressWriteValidator<CreateAddressDto>;
public class UpdateAddressDtoValidator : AddressWriteValidator<UpdateAddressDto>;
