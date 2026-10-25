using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Interfaces.IServices;
using Application.Security;
using Microsoft.Extensions.Options;
namespace Infrastructure.Email;

public sealed class ResendPasswordRecoverySender(HttpClient client, IOptions<PasswordRecoverySettings> settings) : IPasswordRecoveryEmailSender
{
    public async Task SendAsync(string recipient, string secret, CancellationToken ct)
    {
        var options = settings.Value;
        var link = PasswordRecoverySettings.ResetUrl + "?token=" + Uri.EscapeDataString(secret);
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        message.Content = JsonContent.Create(new { from = options.From, to = new[] { recipient }, subject = "S.E.N.T.R.A. — Redefinição de senha",
            text = $"Foi solicitada a redefinição da sua senha no S.E.N.T.R.A.\n\nAbra {link}\n\nO link expira em 15 minutos e pode ser usado uma única vez. Sua senha permanece a mesma até a confirmação. Se não foi você, ignore esta mensagem. Nunca compartilhe este link." });
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        // Never read or log a provider response, exception body, message, secret or recipient.
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Entrega de recuperação não confirmada.");
    }
}
