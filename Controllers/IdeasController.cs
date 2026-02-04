using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models;
using LanzaTuIdea.Api.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

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
            user = await CreateUserFromClaimsAsync(userName, cancellationToken);
            if (user is null)
            {
                return Unauthorized();
            }
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
            user = await CreateUserFromClaimsAsync(userName, cancellationToken);
            if (user is null)
            {
                return Unauthorized();
            }
        }

        await EnsureRoleAsync(user, "Ideador", cancellationToken);
        await UpsertEmployeeAsync(user.Codigo_Empleado, request, user, cancellationToken);

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

    private async Task<AppUser?> CreateUserFromClaimsAsync(string userName, CancellationToken cancellationToken)
    {
        var codigoEmpleado = User.FindFirstValue("CodigoEmpleado");
        var nombreCompleto = User.FindFirstValue("NombreCompleto");

        if (string.IsNullOrWhiteSpace(codigoEmpleado))
        {
            return null;
        }

        var user = new AppUser
        {
            UserName = userName,
            Codigo_Empleado = codigoEmpleado,
            NombreCompleto = nombreCompleto,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task EnsureRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role is null)
        {
            role = new Role { Name = roleName };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var hasRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
        if (!hasRole)
        {
            _context.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task UpsertEmployeeAsync(string? codigoEmpleado, IdeaCreateRequest request, AppUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoEmpleado))
        {
            return;
        }

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Codigo_Empleado == codigoEmpleado, cancellationToken);
        var nombreCompleto = request.NombreCompleto ?? user.NombreCompleto ?? string.Empty;
        var parsed = ParseNombreCompleto(nombreCompleto);
        var email = request.Email ?? string.Empty;
        var departamento = request.Departamento ?? string.Empty;

        if (employee is null)
        {
            employee = new Employee
            {
                Codigo_Empleado = codigoEmpleado,
                Nombre = parsed.Nombre,
                Apellido1 = parsed.Apellido1,
                Apellido2 = parsed.Apellido2,
                E_Mail = email,
                Departamento = departamento,
                Estatus = "A"
            };
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var updated = false;
        if (string.IsNullOrWhiteSpace(employee.Nombre) && !string.IsNullOrWhiteSpace(parsed.Nombre))
        {
            employee.Nombre = parsed.Nombre;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(employee.Apellido1) && !string.IsNullOrWhiteSpace(parsed.Apellido1))
        {
            employee.Apellido1 = parsed.Apellido1;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(employee.Apellido2) && !string.IsNullOrWhiteSpace(parsed.Apellido2))
        {
            employee.Apellido2 = parsed.Apellido2;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(employee.E_Mail) && !string.IsNullOrWhiteSpace(email))
        {
            employee.E_Mail = email;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(employee.Departamento) && !string.IsNullOrWhiteSpace(departamento))
        {
            employee.Departamento = departamento;
            updated = true;
        }

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static (string Nombre, string Apellido1, string Apellido2) ParseNombreCompleto(string nombreCompleto)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return ("", "", "");
        }

        var parts = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "", "");
        }

        if (parts.Length == 2)
        {
            return (parts[0], parts[1], "");
        }

        return (parts[0], parts[1], string.Join(" ", parts.Skip(2)));
    }
}
