using Application.DTOs.Organization;
using FluentValidation;
namespace Application.Validators;

public abstract class OrganizationWriteValidator<T> : AbstractValidator<T> where T : OrganizationWriteDto
{
    protected OrganizationWriteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
public class CreateOrganizationDtoValidator : OrganizationWriteValidator<CreateOrganizationDto>;
public class UpdateOrganizationDtoValidator : OrganizationWriteValidator<UpdateOrganizationDto>;
