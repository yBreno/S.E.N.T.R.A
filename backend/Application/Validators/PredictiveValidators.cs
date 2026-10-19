using Application.Common;
using Application.DTOs.Predictive;
using FluentValidation;
namespace Application.Validators;

public class CreateIncidentDtoValidator : AbstractValidator<CreateIncidentDto>
{
    public CreateIncidentDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Neighborhood).NotEmpty().WithMessage("Bairro e obrigatorio (RD-01).").When(x => x.EquipmentId is null);
        RuleFor(x => x.Neighborhood).MaximumLength(120);
        RuleFor(x => x.Latitude).NotNull().WithMessage("Latitude e obrigatoria (RD-01).").When(x => x.EquipmentId is null);
        RuleFor(x => x.Longitude).NotNull().WithMessage("Longitude e obrigatoria (RD-01).").When(x => x.EquipmentId is null);
        RuleFor(x => x.Latitude).Must(v => Documents.IsValidLatitude(v!.Value)).WithMessage("Latitude deve estar entre -90 e 90.").When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).Must(v => Documents.IsValidLongitude(v!.Value)).WithMessage("Longitude deve estar entre -180 e 180.").When(x => x.Longitude.HasValue);
    }
}

public class CreateTelemetryDtoValidator : AbstractValidator<CreateTelemetryDto>
{
    public CreateTelemetryDtoValidator()
    {
        RuleFor(x => x.EquipmentId).NotEmpty();
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).Must(v => Documents.IsValidLatitude(v!.Value)).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).Must(v => Documents.IsValidLongitude(v!.Value)).When(x => x.Longitude.HasValue);
    }
}

public class UpdateAgentLocationDtoValidator : AbstractValidator<UpdateAgentLocationDto>
{
    public UpdateAgentLocationDtoValidator()
    {
        RuleFor(x => x.Neighborhood).NotEmpty().WithMessage("Bairro e obrigatorio (RD-01).").MaximumLength(120);
        RuleFor(x => x.Latitude).Must(Documents.IsValidLatitude).WithMessage("Latitude deve estar entre -90 e 90 (RD-01).");
        RuleFor(x => x.Longitude).Must(Documents.IsValidLongitude).WithMessage("Longitude deve estar entre -180 e 180 (RD-01).");
        RuleFor(x => x.Availability).IsInEnum().When(x => x.Availability.HasValue);
    }
}
