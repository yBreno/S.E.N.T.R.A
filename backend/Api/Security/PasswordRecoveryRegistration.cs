using Application.Interfaces.IServices;
using Application.Security;
using Infrastructure.Email;
using Api.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Api.Security;

public static class PasswordRecoveryRegistration
{
    public static IServiceCollection AddPasswordRecovery(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, bool demo = false)
    {
        services.AddOptions<PasswordRecoverySettings>().Bind(configuration.GetSection(PasswordRecoverySettings.SectionName))
            .Validate(o => o.IsSafe(environment.EnvironmentName) && (!demo || !o.Enabled), "Configuração de recuperação inválida; verificar provedor, remetente e segredos privados. Transporte local é restrito a Development/Testing.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordRecoveryQueue, PasswordRecoveryQueue>();
        if (configuration.GetValue<string>("PasswordRecovery:Transport") == "LocalTest")
        {
            services.AddSingleton(_ => new LocalTestPasswordRecoverySender(environment.EnvironmentName));
            services.AddSingleton<IPasswordRecoveryEmailSender>(sp => sp.GetRequiredService<LocalTestPasswordRecoverySender>());
        }
        else services.AddHttpClient<IPasswordRecoveryEmailSender, ResendPasswordRecoverySender>(client => client.Timeout = TimeSpan.FromSeconds(8))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddHostedService<PasswordRecoveryWorker>(); return services;
    }
}
