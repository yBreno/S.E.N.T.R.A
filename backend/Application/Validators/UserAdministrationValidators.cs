using Application.DTOs.Auth;
using Application.Security;
using FluentValidation;
namespace Application.Validators;

public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator() { Include(new RegisterDtoValidator()); RuleFor(x => x.Role).NotNull(); }
}
public class UserSearchDtoValidator : AbstractValidator<UserSearchDto>
{
    public UserSearchDtoValidator()
    {
        RuleFor(x => x.Search).MaximumLength(150); RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
{
    public UpdateUserProfileDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Revision).GreaterThan(0);
    }
}
public class ChangeUserRoleDtoValidator : AbstractValidator<ChangeUserRoleDto>
{
    public ChangeUserRoleDtoValidator() { RuleFor(x => x.Role).NotNull().IsInEnum(); RuleFor(x => x.Revision).GreaterThan(0); }
}
public class ChangeUserStatusDtoValidator : AbstractValidator<ChangeUserStatusDto>
{
    public ChangeUserStatusDtoValidator() { RuleFor(x => x.IsActive).NotNull(); RuleFor(x => x.Revision).GreaterThan(0); }
}
public class ChangeOwnPasswordDtoValidator : AbstractValidator<ChangeOwnPasswordDto>
{
    public ChangeOwnPasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().Must(PasswordLimits.Accepts);
        PasswordRules.Apply(RuleFor(x => x.NewPassword));
    }
}
public static class PasswordRules
{
    public static void Apply<T>(IRuleBuilderInitial<T, string> rule) => rule.NotEmpty().MinimumLength(8)
        .Must(PasswordLimits.Accepts).WithMessage("A senha deve ter no máximo 72 bytes UTF-8.")
        .Matches("[A-Z]").WithMessage("A senha deve ter ao menos uma letra maiúscula.")
        .Matches("[a-z]").WithMessage("A senha deve ter ao menos uma letra minúscula.")
        .Matches("[0-9]").WithMessage("A senha deve ter ao menos um número.")
        .Matches("[^a-zA-Z0-9]").WithMessage("A senha deve ter ao menos um caractere especial.");
}
