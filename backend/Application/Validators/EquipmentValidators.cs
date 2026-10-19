using Application.Common;
using Application.DTOs.Equipment;
using Domain.Enums;
using FluentValidation;
namespace Application.Validators;

public abstract class EquipmentWriteValidator<T> : AbstractValidator<T> where T : EquipmentWriteDto
{
    protected EquipmentWriteValidator()
    {
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(80);
        RuleFor(x => x.AddressId).NotEmpty().WithMessage("AddressId e obrigatorio (RD-01).");
        RuleFor(x => x.LicensePlate).Must(Documents.IsValidLicensePlate).WithMessage("Placa invalida.")
            .When(x => !string.IsNullOrWhiteSpace(x.LicensePlate));
        RuleFor(x => x.Chassi).Length(17).When(x => !string.IsNullOrWhiteSpace(x.Chassi));
        RuleFor(x => x.ManufactureYear).InclusiveBetween(1900, DateTime.UtcNow.Year + 1).When(x => x.ManufactureYear.HasValue);
        RuleFor(x => x.MaxSpeed).GreaterThan(0).When(x => x.MaxSpeed.HasValue);
        RuleFor(x => x.MaxSpeedLimit).GreaterThan(0).When(x => x.MaxSpeedLimit.HasValue);
        RuleFor(x => x.Spots).GreaterThan(0).When(x => x.Spots.HasValue);
    }
}
public class CreateEquipmentDtoValidator : EquipmentWriteValidator<CreateEquipmentDto>
{
    public CreateEquipmentDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.LicensePlate).NotEmpty().WithMessage("Placa e obrigatoria para veiculos.")
            .When(x => x.Type is EquipmentType.Vehicle or EquipmentType.Motorcycle or EquipmentType.AutonomousVehicle);
    }
}
public class UpdateEquipmentDtoValidator : EquipmentWriteValidator<UpdateEquipmentDto>;
public class ChangeEquipmentStatusDtoValidator : AbstractValidator<ChangeEquipmentStatusDto>
{
    public ChangeEquipmentStatusDtoValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
