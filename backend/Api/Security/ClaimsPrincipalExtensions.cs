using System.Security.Claims;
namespace Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();

    public static bool IsManager(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.OperationManager);
}
