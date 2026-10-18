using Domain.Entities;
namespace Application.Interfaces.Security;
public record TokenResult(string Token, DateTime ExpiresAt);
public interface ITokenService
{
    TokenResult GenerateToken(User user);
}
