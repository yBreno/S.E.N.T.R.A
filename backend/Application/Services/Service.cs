using Application.Interfaces.IServices;
using Domain.Entities;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

/// <summary>
/// CRUD generico baseado em DTOs. Os services concretos implementam apenas mapeamento e regras.
/// O SaveChanges acontece SOMENTE aqui/nos services (via IUnitOfWork).
/// </summary>
public abstract class Service<TEntity, TCreate, TUpdate, TResponse>(
    IRepository<TEntity> repository,
    IUnitOfWork unitOfWork,
    IValidator<TCreate> createValidator,
    IValidator<TUpdate> updateValidator) : IService<TResponse, TCreate, TUpdate>
    where TEntity : BaseEntity
{
    protected IRepository<TEntity> Repository { get; } = repository;
    protected IUnitOfWork UnitOfWork { get; } = unitOfWork;

    protected abstract string EntityName { get; }
    protected abstract TResponse ToResponse(TEntity entity);
    protected abstract Task<TEntity> BuildAsync(TCreate dto, CancellationToken ct);
    protected abstract Task ApplyUpdateAsync(TEntity entity, TUpdate dto, CancellationToken ct);
    protected virtual Task EnsureCanDeleteAsync(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    protected async Task<TEntity> LoadAsync(Guid id, CancellationToken ct) =>
        await Repository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"{EntityName} {id} nao encontrado(a).");

    public async Task<TResponse> GetByIdAsync(Guid id, CancellationToken ct = default) => ToResponse(await LoadAsync(id, ct));

    public async Task<IReadOnlyList<TResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await Repository.GetAllAsync(ct)).Select(ToResponse).ToList();

    public async Task<TResponse> CreateAsync(TCreate dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        var entity = await BuildAsync(dto, ct);
        await Repository.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        return ToResponse(await LoadAsync(entity.Id, ct));
    }

    public async Task<TResponse> UpdateAsync(Guid id, TUpdate dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        var entity = await LoadAsync(id, ct);
        await ApplyUpdateAsync(entity, dto, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        return ToResponse(await LoadAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        await EnsureCanDeleteAsync(entity, ct);
        Repository.Remove(entity);
        await UnitOfWork.SaveChangesAsync(ct);
    }
}
