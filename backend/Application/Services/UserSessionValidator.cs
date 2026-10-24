using Application.Interfaces.Security;
using Domain.Enums;
using Domain.IRepositories;

namespace Application.Services;

public sealed class UserSessionValidator(IUserRepository users) : IUserSessionValidator
{
    public async Task<bool> IsCurrentAsync(Guid userId, string role, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || !Enum.TryParse<UserRole>(role, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != role) return false;
        return await users.HasRoleAsync(userId, parsed, ct);
    }
}
