using Application.Interfaces.IServices;
using Domain.Exceptions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Tests.Support;

/// <summary>Somente testes InMemory. A aplicação real exige transação e bloqueio no SQL.</summary>
public sealed class InMemoryUserAdministrationCoordinator(AppDbContext db) : IUserAdministrationCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory") throw new InvalidOperationException("Somente InMemory de teste.");
        await Gate.WaitAsync(ct);
        try { return await action(); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new ConflictException("Conta alterada. Atualize os detalhes."); }
        catch { db.ChangeTracker.Clear(); throw; }
        finally { Gate.Release(); }
    }
}
