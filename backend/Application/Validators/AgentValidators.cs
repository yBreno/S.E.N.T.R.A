using Application.Common;
using Application.DTOs.Agent;
using FluentValidation;
namespace Application.Validators;

public abstract class AgentWriteValidator<T> : AbstractValidator<T> where T : AgentWriteDto
{
    protected AgentWriteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BadgeNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Availability).IsInEnum();
        RuleFor(x => x.Neighborhood).NotEmpty().WithMessage("Bairro e obrigatorio (RD-01).").MaximumLength(120);
        RuleFor(x => x.Latitude).Must(Documents.IsValidLatitude).WithMessage("Latitude deve estar entre -90 e 90 (RD-01).");
        RuleFor(x => x.Longitude).Must(Documents.IsValidLongitude).WithMessage("Longitude deve estar entre -180 e 180 (RD-01).");
    }
}
public class CreateAgentDtoValidator : AgentWriteValidator<CreateAgentDto>
{
    public CreateAgentDtoValidator() =>
        RuleFor(x => x.Cpf).Must(Documents.IsValidCpf).WithMessage("CPF invalido.");
}
public class UpdateAgentDtoValidator : AgentWriteValidator<UpdateAgentDto>;
