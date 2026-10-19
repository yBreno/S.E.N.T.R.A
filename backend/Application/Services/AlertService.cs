using Application.DTOs.Alert;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
namespace Application.Services;

public class AlertService(IAlertRepository repo, IUnitOfWork uow) : IAlertService
{
    public async Task<AlertResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Alerta {id} nao encontrado.")).ToResponse();

    public async Task<IReadOnlyList<AlertResponseDto>> GetAllAsync(AlertStatus? status, AlertType? type, CancellationToken ct = default) =>
        (await repo.GetByStatusAsync(status, type, ct)).Select(a => a.ToResponse()).ToList();

    public async Task<AlertResponseDto> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var a = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Alerta {id} nao encontrado.");
        a.Resolve();
        await uow.SaveChangesAsync(ct);
        return a.ToResponse();
    }
}
