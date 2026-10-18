using Application.DTOs.Alert;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
namespace Application.Services;

public class AlertService(IAlertRepository repo, IUnitOfWork uow) : IAlertService
{
    private async Task<Domain.Entities.Alert> LoadAsync(Guid id, CancellationToken ct) =>
        await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Alerta {id} nao encontrado.");

    public async Task<AlertResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) => (await LoadAsync(id, ct)).ToResponse();

    public async Task<IReadOnlyList<AlertResponseDto>> GetAllAsync(AlertStatus? status, CancellationToken ct = default) =>
        (await repo.GetByStatusAsync(status, ct)).Select(a => a.ToResponse()).ToList();

    public async Task<AlertResponseDto> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await LoadAsync(id, ct);
        alert.Resolve();
        await uow.SaveChangesAsync(ct);
        return alert.ToResponse();
    }
}
