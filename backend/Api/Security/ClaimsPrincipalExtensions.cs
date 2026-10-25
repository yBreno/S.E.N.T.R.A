using System.Security.Claims;
using Domain.Exceptions;
using Application.DTOs.Auth;
using System.Globalization;
namespace Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : throw new UnauthorizedException("Identidade de sessão inválida.");

    public static bool IsManager(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.OperationManager);
    public static AccountActor GetAccountActor(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("auth_version"), NumberStyles.None, CultureInfo.InvariantCulture, out var version) && version > 0
            ? new(user.GetUserId(), version) : throw new UnauthorizedException("Versão de sessão inválida.");
    public static bool IsMonitoring(this ClaimsPrincipal user) => user.IsManager() || user.IsInRole(Roles.CentralOperator);
    public static bool IsMaintenanceOnly(this ClaimsPrincipal user) => user.IsInRole(Roles.MaintenanceTechnician) && !user.IsMonitoring();
}
