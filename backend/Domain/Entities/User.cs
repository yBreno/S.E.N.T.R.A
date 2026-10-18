using Domain.Enums;
namespace Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Cpf { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    private User() { } // EF Core

    public static User Create(string name, string email, string cpf, string passwordHash, UserRole role) => new()
    {
        Name = name.Trim(),
        Email = email.Trim().ToLowerInvariant(),
        Cpf = new string(cpf.Where(char.IsDigit).ToArray()),
        PasswordHash = passwordHash,
        Role = role
    };

    public void ChangeRole(UserRole role) => Role = role;
    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
