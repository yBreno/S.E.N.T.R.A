using Application.Interfaces.IServices;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Infrastructure.Data;

/// <summary>Mesmo recurso SQL para análise e todos os geradores de recomendações desta versão.</summary>
public sealed class SqlPredictiveWriteCoordinator(AppDbContext db) : IPredictiveWriteCoordinator
{
    public bool SupportsMultipleInstances => db.Database.IsSqlServer();
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (!SupportsMultipleInstances) throw new InvalidOperationException("Coordenação preditiva exige SQL Server.");
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = N'SENTRA.PredictiveWrites.v1', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @result;";
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (result < 0) throw new ConflictException("Outra execução preditiva está em andamento. Tente novamente após a conclusão.");
            var value = await action();
            ct.ThrowIfCancellationRequested();
            if (transaction is not null) await transaction.CommitAsync(ct);
            return value;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
