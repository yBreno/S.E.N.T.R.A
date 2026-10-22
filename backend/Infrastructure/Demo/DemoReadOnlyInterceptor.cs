using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Demo;

/// <summary>Defense against accidental service writes, registered only by the Demo HTTP host.</summary>
public sealed class DemoReadOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new InvalidOperationException("Persistencia bloqueada na API HTTP DEMO.");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Persistencia bloqueada na API HTTP DEMO.");
}
