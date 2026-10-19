using Api.Security;
using Application.DTOs.Equipment;
using Application.Interfaces.IServices;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/equipments")]
[Authorize]
public class EquipmentsController(IEquipmentService service) : ControllerBase
{
    /// <summary>RF-04: filtros opcionais por tipo, status e bairro (retorna coordenadas).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<EquipmentResponseDto>>> Search([FromQuery] EquipmentType? type, [FromQuery] EquipmentStatus? status,
        [FromQuery] string? neighborhood, CancellationToken ct) =>
        Ok(await service.SearchAsync(new EquipmentSearchDto(type, status, neighborhood), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<EquipmentResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<EquipmentResponseDto>> Create([FromBody] CreateEquipmentDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<EquipmentResponseDto>> Update(Guid id, [FromBody] UpdateEquipmentDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    /// <summary>RD-02: transicao de status. Deactivated -> Operational exige Admin/OperationManager.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<EquipmentResponseDto>> ChangeStatus(Guid id, [FromBody] ChangeEquipmentStatusDto dto, CancellationToken ct) =>
        Ok(await service.ChangeStatusAsync(id, dto, User.IsManager(), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
