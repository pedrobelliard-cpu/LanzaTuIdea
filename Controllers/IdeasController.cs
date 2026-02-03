using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanzaTuIdea.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ideas")]
public class IdeasController : ControllerBase
{
    private readonly AppDbContext _context;

    public IdeasController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<IdeaSummaryDto>>> Mine(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var ideas = await _context.Ideas
            .Where(i => i.CreatedByUserId == user.Id)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IdeaSummaryDto(i.Id, i.CreatedAt, i.Descripcion, i.Status))
            .ToListAsync(cancellationToken);

        return ideas;
    }

    [HttpPost]
    public async Task<ActionResult<IdeaSummaryDto>> Create([FromBody] IdeaCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Descripcion) || string.IsNullOrWhiteSpace(request.Detalle))
        {
            return BadRequest(new { message = "Descripción y detalle son requeridos." });
        }

        if (request.Descripcion.Length > 500 || request.Detalle.Length > 4000)
        {
            return BadRequest(new { message = "Descripción o detalle exceden el límite permitido." });
        }

        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var codigoEmpleado = user.Codigo_Empleado ?? string.Empty;
        var idea = new Models.Idea
        {
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id,
            CodigoEmpleado = codigoEmpleado,
            Descripcion = request.Descripcion.Trim(),
            Detalle = request.Detalle.Trim(),
            Status = "Registrada",
            Via = "Sistema"
        };

        idea.History.Add(new Models.IdeaHistory
        {
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = user.Id,
            ChangeType = "Creación",
            Notes = "Registro inicial"
        });

        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new IdeaSummaryDto(idea.Id, idea.CreatedAt, idea.Descripcion, idea.Status);
        return CreatedAtAction(nameof(GetById), new { id = idea.Id }, dto);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IdeaDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var idea = await _context.Ideas
            .Include(i => i.CreatedByUser)
            .Include(i => i.History)
            .ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (idea is null)
        {
            return NotFound();
        }

        if (idea.CreatedByUserId != user.Id)
        {
            return Forbid();
        }

        var history = idea.History
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new IdeaHistoryDto(
                h.ChangedAt,
                h.ChangedByUser.NombreCompleto ?? h.ChangedByUser.UserName,
                h.ChangeType,
                h.Notes))
            .ToList();

        return new IdeaDetailDto(
            idea.Id,
            idea.CreatedAt,
            idea.Descripcion,
            idea.Detalle,
            idea.Status,
            idea.Clasificacion,
            idea.Via,
            idea.AdminComment,
            idea.CodigoEmpleado,
            idea.CreatedByUser.NombreCompleto,
            history);
    }
}
