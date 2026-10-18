using Api.Security;
using Application.DTOs.Organization;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<OrganizationResponseDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<OrganizationResponseDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OrganizationResponseDto>> Create([FromBody] CreateOrganizationDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OrganizationResponseDto>> Update(Guid id, [FromBody] UpdateOrganizationDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
