using Application.Common;
using Application.DTOs.Agent;
using Application.Interfaces.IServices;
using Application.Mappings;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class AgentService(IAgentRepository repo, IOrganizationRepository orgs, IUserRepository users, IUnitOfWork uow,
    IValidator<CreateAgentDto> cv, IValidator<UpdateAgentDto> uv)
    : Service<Domain.Entities.Agent, CreateAgentDto, UpdateAgentDto, AgentResponseDto>(repo, uow, cv, uv), IAgentService
{
    protected override string EntityName => "Agente";
    protected override AgentResponseDto ToResponse(Domain.Entities.Agent e) => e.ToResponse();

    private async Task CheckReferencesAsync(AgentWriteDto dto, Guid? ignoreId, CancellationToken ct)
    {
        if (!await orgs.ExistsAsync(dto.OrganizationId, ct)) throw new NotFoundException("Organizacao nao encontrada.");
        if (await repo.BadgeExistsAsync(dto.BadgeNumber.Trim(), ignoreId, ct)) throw new ConflictException("Matricula ja cadastrada.");
        if (dto.UserId is { } uid)
        {
            if (!await users.ExistsAsync(uid, ct)) throw new NotFoundException("Usuario nao encontrado.");
            if (await repo.UserLinkedAsync(uid, ignoreId, ct)) throw new ConflictException("Usuario ja vinculado a outro agente.");
        }
    }

    protected override async Task<Domain.Entities.Agent> BuildAsync(CreateAgentDto dto, CancellationToken ct)
    {
        var cpf = Documents.Digits(dto.Cpf);
        if (await repo.CpfExistsAsync(cpf, null, ct)) throw new ConflictException("CPF ja cadastrado.");
        await CheckReferencesAsync(dto, null, ct);
        var a = new Domain.Entities.Agent { Cpf = cpf };
        a.Apply(dto);
        return a;
    }

    protected override async Task ApplyUpdateAsync(Domain.Entities.Agent e, UpdateAgentDto dto, CancellationToken ct)
    {
        await CheckReferencesAsync(dto, e.Id, ct);
        e.Apply(dto);
    }

    protected override async Task EnsureCanDeleteAsync(Domain.Entities.Agent e, CancellationToken ct)
    {
        if (await repo.HasDependentsAsync(e.Id, ct)) throw new ConflictException("Agente possui manutencoes/veiculos vinculados.");
    }

    public async Task<AgentResponseDto> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        (await repo.GetByUserIdAsync(userId, ct) ?? throw new NotFoundException("Nenhum agente vinculado ao usuario.")).ToResponse();
}
