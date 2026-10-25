using System.Threading.Channels;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
namespace Infrastructure.Email;

public sealed class PasswordRecoveryQueue : IPasswordRecoveryQueue
{
    private readonly Channel<PasswordRecoveryRequest> channel = Channel.CreateBounded<PasswordRecoveryRequest>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    public bool TryEnqueue(PasswordRecoveryRequest request) => channel.Writer.TryWrite(request);
    public IAsyncEnumerable<PasswordRecoveryRequest> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);
}
