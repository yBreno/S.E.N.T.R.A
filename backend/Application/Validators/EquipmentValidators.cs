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
        RuleFor(x => x.Category).MaximumLength(80);
        RuleFor(x => x.AddressId).NotEmpty().WithMessage("Selecione ou informe uma localizacao.").When(x => x.Address is null);
        RuleFor(x => x.AddressId).Empty().WithMessage("Informe Address ou AddressId, nunca ambos.").When(x => x.Address is not null);
        RuleFor(x => x.Address).SetValidator(new CreateAddressDtoValidator()!).When(x => x.Address is not null);
        RuleFor(x => x.MaintenanceNeed).IsInEnum().When(x => x.MaintenanceNeed.HasValue);
        RuleFor(x => x.NextMaintenanceDate).NotNull().WithMessage("Informe a proxima data prevista.")
            .When(x => x.MaintenanceNeed is MaintenanceNeed.Scheduled or MaintenanceNeed.Periodic);
        RuleFor(x => x.NextMaintenanceDate).Null().WithMessage("Uma data exige necessidade agendada ou periodica.")
            .When(x => x.MaintenanceNeed is null or MaintenanceNeed.None or MaintenanceNeed.Immediate);
        RuleFor(x => x.Resolution).MaximumLength(20);
        RuleFor(x => x.RadarType).MaximumLength(60);
        RuleFor(x => x.SensorType).MaximumLength(60);
        RuleFor(x => x.MeasurementUnit).MaximumLength(30);
        RuleFor(x => x.BatteryCapacity).MaximumLength(30);
        RuleFor(x => x.LicensePlate).Must(Documents.IsValidLicensePlate).WithMessage("Placa invalida.")
            .When(x => !string.IsNullOrWhiteSpace(x.LicensePlate));
        RuleFor(x => x.Chassi).Length(17).When(x => !string.IsNullOrWhiteSpace(x.Chassi));
        RuleFor(x => x.ManufactureYear).InclusiveBetween(1900, DateTime.UtcNow.Year + 1).When(x => x.ManufactureYear.HasValue);
        RuleFor(x => x.MaxSpeed).GreaterThan(0).When(x => x.MaxSpeed.HasValue);
        RuleFor(x => x.MaxSpeedLimit).GreaterThan(0).When(x => x.MaxSpeedLimit.HasValue);
        RuleFor(x => x.Spots).GreaterThan(0).When(x => x.Spots.HasValue);
        RuleFor(x => x.EngineCapacityCc).GreaterThan(0).When(x => x.EngineCapacityCc.HasValue);
        RuleFor(x => x.AutonomyLevel).InclusiveBetween(0, 5).When(x => x.AutonomyLevel.HasValue);
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
