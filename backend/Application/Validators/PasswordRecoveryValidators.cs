using Application.DTOs.Auth;
using FluentValidation;
namespace Application.Validators;

public sealed class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
}
public sealed class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Matches("^[A-Za-z0-9_-]{43}$");
        PasswordRules.Apply(RuleFor(x => x.NewPassword));
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("A confirmação deve ser igual à nova senha.");
    }
}
