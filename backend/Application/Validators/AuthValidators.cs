using Application.Common;
using Application.DTOs.Auth;
using Application.Security;
using FluentValidation;
namespace Application.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Cpf).Must(Documents.IsValidCpf).WithMessage("CPF invalido.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Must(PasswordLimits.Accepts).WithMessage("A senha deve ter no máximo 72 bytes UTF-8.")
            .Matches("[A-Z]").WithMessage("A senha deve ter ao menos uma letra maiuscula.")
            .Matches("[a-z]").WithMessage("A senha deve ter ao menos uma letra minuscula.")
            .Matches("[0-9]").WithMessage("A senha deve ter ao menos um numero.")
            .Matches("[^a-zA-Z0-9]").WithMessage("A senha deve ter ao menos um caractere especial.");
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
    }
}
public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().Must(PasswordLimits.Accepts).WithMessage("A senha deve ter no máximo 72 bytes UTF-8.");
    }
}
