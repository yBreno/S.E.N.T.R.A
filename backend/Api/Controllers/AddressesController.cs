using Api.Security;
using Application.DTOs.Address;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressesController(IAddressService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<AddressResponseDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AddressResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<AddressResponseDto>> Create([FromBody] CreateAddressDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<ActionResult<AddressResponseDto>> Update(Guid id, [FromBody] UpdateAddressDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
