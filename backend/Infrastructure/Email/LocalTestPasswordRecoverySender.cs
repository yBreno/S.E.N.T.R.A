using System.Collections.Concurrent;
using Application.Interfaces.IServices;
namespace Infrastructure.Email;

/// <summary>Memory only, bounded, no public HTTP/mailbox endpoint. For automated or local tests only.</summary>
public sealed class LocalTestPasswordRecoverySender : IPasswordRecoveryEmailSender
{
    private readonly ConcurrentQueue<TestMessage> messages = new();
    public LocalTestPasswordRecoverySender(string environment)
    {
        if (environment is not ("Testing" or "Development")) throw new InvalidOperationException("Transporte local recusado neste ambiente.");
    }
    public TestMessage[] Messages => messages.ToArray();
    public bool FailDelivery { get; set; }
    public Task SendAsync(string recipient, string secret, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailDelivery) throw new InvalidOperationException("Falha sintética de transporte.");
        messages.Enqueue(new(recipient, secret)); while (messages.Count > 20) messages.TryDequeue(out _);
        return Task.CompletedTask;
    }
    public sealed class TestMessage(string recipient, string secret)
    {
        public string Recipient { get; } = recipient;
        public string Secret { get; } = secret;
        public override string ToString() => nameof(TestMessage);
    }
}
