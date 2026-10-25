using Domain.Enums;
using Domain.Exceptions;
namespace Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Cpf { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long AuthVersion { get; private set; } = 1;
    public long Revision { get; private set; } = 1;

    private User() { } // EF Core

    public static User Create(string name, string email, string cpf, string passwordHash, UserRole role)
    {
        CheckRole(role);
        return new()
        {
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Cpf = new string(cpf.Where(char.IsDigit).ToArray()),
            PasswordHash = passwordHash,
            Role = role
        };
    }

    public void ChangeRole(UserRole role)
    {
        CheckRole(role);
        if (Role == role) return;
        Role = role; RevokeSessions();
    }
    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active; RevokeSessions();
    }
    public void UpdateProfile(string name, string email)
    {
        name = name.Trim(); email = email.Trim().ToLowerInvariant();
        if (Name == name && Email == email) return;
        if (Email != email) AuthVersion = checked(AuthVersion + 1);
        Name = name; Email = email; Revision = checked(Revision + 1);
    }
    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash; RevokeSessions();
    }
    private void RevokeSessions() { AuthVersion = checked(AuthVersion + 1); Revision = checked(Revision + 1); }
    private static void CheckRole(UserRole role)
    {
        if (!Enum.IsDefined(role)) throw new BusinessRuleException("Cargo de autorização desconhecido.");
    }
}
