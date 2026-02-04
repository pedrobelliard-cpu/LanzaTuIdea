using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models;
using LanzaTuIdea.Api.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanzaTuIdea.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConfigController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("classifications")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> GetClassifications(CancellationToken cancellationToken)
    {
        var items = await _context.Classifications
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CatalogItemDto(c.Id, c.Nombre))
            .ToListAsync(cancellationToken);

        return items;
    }

    [HttpPost("classifications")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CatalogItemDto>> CreateClassification([FromBody] CreateCatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return BadRequest(new { message = "El nombre es requerido." });
        }

        var classification = new Classification { Nombre = request.Nombre.Trim(), Activo = true };
        _context.Classifications.Add(classification);
        await _context.SaveChangesAsync(cancellationToken);
        return new CatalogItemDto(classification.Id, classification.Nombre);
    }

    [HttpDelete("classifications/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClassification(int id, CancellationToken cancellationToken)
    {
        var classification = await _context.Classifications.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (classification is null)
        {
            return NotFound();
        }

        classification.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("instances")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> GetInstances(CancellationToken cancellationToken)
    {
        var items = await _context.Instances
            .AsNoTracking()
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new CatalogItemDto(i.Id, i.Nombre))
            .ToListAsync(cancellationToken);

        return items;
    }

    [HttpPost("instances")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CatalogItemDto>> CreateInstance([FromBody] CreateCatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return BadRequest(new { message = "El nombre es requerido." });
        }

        var instance = new Instance { Nombre = request.Nombre.Trim(), Activo = true };
        _context.Instances.Add(instance);
        await _context.SaveChangesAsync(cancellationToken);
        return new CatalogItemDto(instance.Id, instance.Nombre);
    }

    [HttpDelete("instances/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteInstance(int id, CancellationToken cancellationToken)
    {
        var instance = await _context.Instances.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        instance.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
