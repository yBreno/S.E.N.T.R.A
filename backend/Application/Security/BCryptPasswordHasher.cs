using Application.Interfaces.Security;
using Domain.Exceptions;
namespace Application.Security;
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        if (!PasswordLimits.Accepts(password)) throw new BusinessRuleException("A senha excede o limite de 72 bytes UTF-8.");
        return BCrypt.Net.BCrypt.HashPassword(password, 11);
    }
    public bool Verify(string password, string hash) => PasswordLimits.Accepts(password) && BCrypt.Net.BCrypt.Verify(password, hash);
}
