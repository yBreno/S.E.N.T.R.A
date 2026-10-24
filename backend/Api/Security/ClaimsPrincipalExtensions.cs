using System.Security.Claims;
using Domain.Exceptions;
namespace Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : throw new UnauthorizedException("Identidade de sessão inválida.");

    public static bool IsManager(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.OperationManager);
    public static bool IsMonitoring(this ClaimsPrincipal user) => user.IsManager() || user.IsInRole(Roles.CentralOperator);
    public static bool IsMaintenanceOnly(this ClaimsPrincipal user) => user.IsInRole(Roles.MaintenanceTechnician) && !user.IsMonitoring();
}
