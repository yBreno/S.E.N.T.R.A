using System.Text;
using Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api.Security;

public static class JwtAuthentication
{
    public static IServiceCollection AddSentraJwt(this IServiceCollection services, JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Key) || settings.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key ausente ou curta (mínimo 32 caracteres). Configure a chave privadamente.");

        services.Configure<JwtSettings>(options =>
        {
            options.Key = settings.Key; options.Issuer = settings.Issuer;
            options.Audience = settings.Audience; options.ExpiresMinutes = settings.ExpiresMinutes;
        });
        services.AddScoped<CurrentUserJwtEvents>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.EventsType = typeof(CurrentUserJwtEvents);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer,
                ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub", RoleClaimType = "role"
            };
        });
        return services;
    }
}
