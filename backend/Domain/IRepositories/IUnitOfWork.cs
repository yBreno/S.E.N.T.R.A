namespace Domain.IRepositories;

/// <summary>Unico ponto de persistencia. Apenas os services chamam SaveChangesAsync.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
