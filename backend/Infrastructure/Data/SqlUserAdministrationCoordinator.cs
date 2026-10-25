using System.Data;
using Application.Interfaces.IServices;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Infrastructure.Data;

public sealed class SqlUserAdministrationCoordinator(AppDbContext db) : IUserAdministrationCoordinator
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Administração exige transação SQL exclusiva.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'SENTRA.UserAdministration.v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r;";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0) throw new ConflictException("Outra alteração de conta está em andamento. Atualize e tente novamente.");
            var result = await action(); ct.ThrowIfCancellationRequested(); await transaction.CommitAsync(ct); return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            throw new ConflictException("A conta foi alterada por outra operação. Atualize os detalhes.");
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}
