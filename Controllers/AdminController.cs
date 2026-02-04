using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models;
using LanzaTuIdea.Api.Models.Dto;
using LanzaTuIdea.Api.Services;
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
    private readonly IAdServiceClient _adServiceClient;

    public AdminController(AppDbContext context, IAdServiceClient adServiceClient)
    {
        _context = context;
        _adServiceClient = adServiceClient;
    }

    [HttpGet("ideas/pending")]
    public async Task<ActionResult<IReadOnlyList<IdeaAdminSummaryDto>>> PendingIdeas(CancellationToken cancellationToken)
    {
        var ideas = await GetAdminIdeaQuery(_context.Ideas
                .AsNoTracking()
                .Where(i => i.Status == "Registrada")
                .OrderByDescending(i => i.CreatedAt))
            .ToListAsync(cancellationToken);

        return ideas;
    }

    [HttpGet("ideas/reviewed")]
    public async Task<ActionResult<IReadOnlyList<IdeaAdminSummaryDto>>> ReviewedIdeas(CancellationToken cancellationToken)
    {
        var ideas = await GetAdminIdeaQuery(_context.Ideas
                .AsNoTracking()
                .Where(i => i.Status != "Registrada")
                .OrderByDescending(i => i.CreatedAt))
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

        await UpsertEmployeeAsync(request, cancellationToken);

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

    [HttpPost("users")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest(new { message = "El nombre de usuario es requerido." });
        }

        var normalized = request.UserName.Trim();
        var existing = await _context.AppUsers.FirstOrDefaultAsync(u => u.UserName == normalized, cancellationToken);
        if (existing is not null)
        {
            return Conflict(new { message = "El usuario ya existe." });
        }

        var adData = await _adServiceClient.GetUserDataAsync(normalized, cancellationToken);
        if (adData is null)
        {
            return BadRequest(new { message = "No fue posible obtener los datos del usuario desde AD." });
        }

        var user = new AppUser
        {
            UserName = normalized,
            IsActive = true,
            Codigo_Empleado = adData.CodigoEmpleado,
            NombreCompleto = adData.NombreCompleto,
            LastLoginAt = null
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            await AssignRoleAsync(user, request.Role.Trim(), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        return new UserSummaryDto(user.UserName, user.Codigo_Empleado, user.NombreCompleto, user.IsActive, roles);
    }

    [HttpDelete("users/{userName}")]
    public async Task<IActionResult> DeleteUser(string userName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return BadRequest(new { message = "El nombre de usuario es requerido." });
        }

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        user.UserRoles.Clear();
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("users/{userName}/roles")]
    public async Task<IActionResult> UpdateRoles(string userName, [FromBody] UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Gestor" };
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

    private IQueryable<IdeaAdminSummaryDto> GetAdminIdeaQuery(IQueryable<Idea> ideas)
    {
        return from idea in ideas
               join user in _context.AppUsers.AsNoTracking() on idea.CreatedByUserId equals user.Id into userGroup
               from user in userGroup.DefaultIfEmpty()
               join employee in _context.Employees.AsNoTracking() on idea.CodigoEmpleado equals employee.Codigo_Empleado into empGroup
               from employee in empGroup.DefaultIfEmpty()
               select new IdeaAdminSummaryDto(
                   idea.Id,
                   idea.CreatedAt,
                   idea.Descripcion,
                   idea.Status,
                   idea.CodigoEmpleado,
                   employee != null
                       ? (employee.Nombre + " " + employee.Apellido1 + " " + employee.Apellido2)
                       : (user != null ? user.NombreCompleto : null),
                   employee != null ? employee.E_Mail : null,
                   employee != null ? employee.Departamento : null,
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

    private async Task UpsertEmployeeAsync(IdeaManualRequest request, CancellationToken cancellationToken)
    {
        var codigo = request.CodigoEmpleado.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return;
        }

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Codigo_Empleado == codigo, cancellationToken);
        var nombreCompleto = request.NombreCompleto ?? string.Empty;
        var parsed = ParseNombreCompleto(nombreCompleto);
        var email = request.Email ?? string.Empty;
        var departamento = request.Departamento ?? string.Empty;

        if (employee is null)
        {
            employee = new Employee
            {
                Codigo_Empleado = codigo,
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

    private async Task AssignRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Gestor" };
        if (!allowedRoles.Contains(roleName))
        {
            return;
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role is null)
        {
            role = new Role { Name = roleName };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
        {
            user.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
        }
    }
}
