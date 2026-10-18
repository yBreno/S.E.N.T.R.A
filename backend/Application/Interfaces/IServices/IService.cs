namespace Application.Interfaces.IServices;

public interface IService<TResponse, in TCreate, in TUpdate>
{
    Task<TResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TResponse>> GetAllAsync(CancellationToken ct = default);
    Task<TResponse> CreateAsync(TCreate dto, CancellationToken ct = default);
    Task<TResponse> UpdateAsync(Guid id, TUpdate dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
