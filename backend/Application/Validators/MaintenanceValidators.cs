using Application.DTOs.Maintenance;
using FluentValidation;
namespace Application.Validators;

public class OpenMaintenanceDtoValidator : AbstractValidator<OpenMaintenanceDto>
{
    public OpenMaintenanceDtoValidator()
    {
        RuleFor(x => x.EquipmentId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Urgency).IsInEnum();
        RuleFor(x => x.PeriodicityDays).InclusiveBetween(1, 36500).When(x => x.PeriodicityDays.HasValue);
    }
}
public class CloseMaintenanceDtoValidator : AbstractValidator<CloseMaintenanceDto>
{
    public CloseMaintenanceDtoValidator() =>
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
}
public class ScheduleMaintenanceDtoValidator : AbstractValidator<ScheduleMaintenanceDto>
{
    public ScheduleMaintenanceDtoValidator()
    {
        Include(new OpenMaintenanceDtoValidator());
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ScheduledDate).Must(d => d.Kind == DateTimeKind.Utc && d >= DateTime.UtcNow.AddMinutes(-1));
        RuleFor(x => x.InitDate).Null();
    }
}
