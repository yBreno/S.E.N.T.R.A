using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context = context;
    protected DbSet<T> Set => Context.Set<T>();

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => Set.FirstOrDefaultAsync(x => x.Id == id, ct);
    public virtual Task<List<T>> GetAllAsync(CancellationToken ct = default) => Set.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Set.AnyAsync(x => x.Id == id, ct);
    public async Task AddAsync(T entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);
    public void Update(T entity) => Set.Update(entity);
    public void Remove(T entity) => Set.Remove(entity);
}
