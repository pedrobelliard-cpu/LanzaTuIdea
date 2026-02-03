using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models;
using LanzaTuIdea.Api.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LanzaTuIdea.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("ideas/pending")]
    public async Task<ActionResult<IReadOnlyList<IdeaAdminSummaryDto>>> PendingIdeas(CancellationToken cancellationToken)
    {
        var ideas = await GetAdminIdeaQuery()
            .Where(i => i.Status == "Registrada")
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return ideas;
    }

    [HttpGet("ideas/reviewed")]
    public async Task<ActionResult<IReadOnlyList<IdeaAdminSummaryDto>>> ReviewedIdeas(CancellationToken cancellationToken)
    {
        var ideas = await GetAdminIdeaQuery()
            .Where(i => i.Status != "Registrada")
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return ideas;
    }

    [HttpGet("ideas/{id:int}")]
    public async Task<ActionResult<IdeaDetailDto>> GetIdea(int id, CancellationToken cancellationToken)
    {
        var idea = await _context.Ideas
            .Include(i => i.CreatedByUser)
            .Include(i => i.History)
            .ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (idea is null)
        {
            return NotFound();
        }

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Codigo_Empleado == idea.CodigoEmpleado, cancellationToken);
        var nombreCompleto = employee?.NombreCompleto ?? idea.CreatedByUser.NombreCompleto;

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
            nombreCompleto,
            history);
    }

    [HttpPut("ideas/{id:int}/review")]
    public async Task<IActionResult> ReviewIdea(int id, [FromBody] IdeaReviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "El estatus es requerido." });
        }

        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        idea.Status = request.Status.Trim();
        idea.Clasificacion = request.Clasificacion?.Trim();
        idea.AdminComment = request.AdminComment?.Trim();

        var adminUser = await GetCurrentUserAsync(cancellationToken);
        if (adminUser is null)
        {
            return Unauthorized();
        }

        idea.History.Add(new IdeaHistory
        {
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = adminUser.Id,
            ChangeType = "Revisión",
            Notes = request.AdminComment?.Trim()
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("ideas/manual")]
    public async Task<IActionResult> ManualIdea([FromBody] IdeaManualRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoEmpleado) || string.IsNullOrWhiteSpace(request.Descripcion) || string.IsNullOrWhiteSpace(request.Detalle))
        {
            return BadRequest(new { message = "Código de empleado, descripción y detalle son requeridos." });
        }

        var adminUser = await GetCurrentUserAsync(cancellationToken);
        if (adminUser is null)
        {
            return Unauthorized();
        }

        var idea = new Idea
        {
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUser.Id,
            CodigoEmpleado = request.CodigoEmpleado.Trim(),
            Descripcion = request.Descripcion.Trim(),
            Detalle = request.Detalle.Trim(),
            Status = "Revisada",
            Clasificacion = "Manual Admin",
            Via = request.Via?.Trim() ?? "Manual",
            AdminComment = request.AdminComment?.Trim() ?? "Carga manual"
        };

        idea.History.Add(new IdeaHistory
        {
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = adminUser.Id,
            ChangeType = "Registro Manual Administrativo",
            Notes = idea.AdminComment
        });

        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken cancellationToken)
    {
        var total = await _context.Ideas.CountAsync(cancellationToken);
        var pendientes = await _context.Ideas.CountAsync(i => i.Status == "Registrada", cancellationToken);
        var revisadas = await _context.Ideas.CountAsync(i => i.Status != "Registrada", cancellationToken);
        var usuariosActivos = await _context.AppUsers.CountAsync(u => u.IsActive, cancellationToken);

        var porStatus = await _context.Ideas
            .GroupBy(i => i.Status)
            .Select(g => new CountByLabelDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var porClasificacion = await _context.Ideas
            .GroupBy(i => i.Clasificacion ?? "Sin Clasificar")
            .Select(g => new CountByLabelDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return new DashboardDto(total, pendientes, revisadas, usuariosActivos, porStatus, porClasificacion);
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Users(CancellationToken cancellationToken)
    {
        var users = await _context.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var result = users.Select(u => new UserSummaryDto(
            u.UserName,
            u.Codigo_Empleado,
            u.NombreCompleto,
            u.IsActive,
            u.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList()
        )).ToList();

        return result;
    }

    [HttpPut("users/{userName}/roles")]
    public async Task<IActionResult> UpdateRoles(string userName, [FromBody] UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Ideador" };
        var requestedRoles = request.Roles.Where(r => allowedRoles.Contains(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var roles = await _context.Roles.Where(r => allowedRoles.Contains(r.Name)).ToListAsync(cancellationToken);

        user.UserRoles.RemoveAll(ur => !requestedRoles.Contains(ur.Role.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var roleName in requestedRoles)
        {
            var role = roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                role = new Role { Name = roleName };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync(cancellationToken);
                roles.Add(role);
            }

            if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
            {
                user.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("users/{userName}/active")]
    public async Task<IActionResult> UpdateActive(string userName, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("employees/search")]
    public async Task<ActionResult<EmployeeLookupDto>> SearchEmployee([FromQuery] string codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new { message = "El código es requerido." });
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Codigo_Empleado == codigo.Trim(), cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        return new EmployeeLookupDto(
            employee.Codigo_Empleado,
            employee.NombreCompleto,
            employee.E_Mail,
            employee.Departamento);
    }

    private IQueryable<IdeaAdminSummaryDto> GetAdminIdeaQuery()
    {
        return from idea in _context.Ideas.Include(i => i.CreatedByUser)
               join employee in _context.Employees on idea.CodigoEmpleado equals employee.Codigo_Empleado into empGroup
               from employee in empGroup.DefaultIfEmpty()
               select new IdeaAdminSummaryDto(
                   idea.Id,
                   idea.CreatedAt,
                   idea.Descripcion,
                   idea.Status,
                   idea.CodigoEmpleado,
                   employee != null ? employee.NombreCompleto : idea.CreatedByUser.NombreCompleto,
                   idea.Clasificacion
               );
    }

    private async Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }
}
