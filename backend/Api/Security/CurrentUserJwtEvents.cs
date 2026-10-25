using Application.Interfaces.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Globalization;

namespace Api.Security;

/// <summary>Executado após assinatura, issuer, audience e expiração terem sido validados.</summary>
public sealed class CurrentUserJwtEvents(IUserSessionValidator sessions) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var subjects = context.Principal?.FindAll("sub").ToArray() ?? [];
        var roles = context.Principal?.FindAll("role").ToArray() ?? [];
        var versions = context.Principal?.FindAll("auth_version").ToArray() ?? [];
        if (subjects.Length != 1 || roles.Length != 1 || versions.Length != 1 || !Guid.TryParse(subjects[0].Value, out var id) ||
            !long.TryParse(versions[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
            !await sessions.IsCurrentAsync(id, roles[0].Value, version, context.HttpContext.RequestAborted))
            context.Fail("Sessão inválida ou desatualizada. Entre novamente.");
    }
}
