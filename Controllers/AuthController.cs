using System.Security.Claims;
using System.Linq;
using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models;
using LanzaTuIdea.Api.Models.Dto;
using LanzaTuIdea.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanzaTuIdea.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAdServiceClient _adServiceClient;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public AuthController(AppDbContext context, IAdServiceClient adServiceClient, IConfiguration configuration, IHostEnvironment environment)
    {
        _context = context;
        _adServiceClient = adServiceClient;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Usuario y contraseña son requeridos." });
        }

        var requestUserName = request.UserName.Trim();
        var normalizedUserName = NormalizeUserName(requestUserName);
        var authenticated = await _adServiceClient.AuthenticateAsync(requestUserName, request.Password, cancellationToken);
        if (!authenticated)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var adData = await _adServiceClient.GetUserDataAsync(requestUserName, cancellationToken);
        if (adData is null)
        {
            return Unauthorized(new { message = "No fue posible obtener los datos del usuario." });
        }

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == normalizedUserName, cancellationToken);

        if (user is not null && !user.IsActive)
        {
            return Forbid();
        }

        if (user is null)
        {
            user = new AppUser
            {
                UserName = normalizedUserName,
                IsActive = true
            };
            _context.AppUsers.Add(user);
        }

        user.Codigo_Empleado = TrimTo(adData.CodigoEmpleado, 20);
        user.NombreCompleto = TrimTo(adData.NombreCompleto, 200);
        user.LastLoginAt = DateTime.UtcNow;

        await EnsureRoleAsync(user, AppConstants.Roles.Ideador, cancellationToken);

        if (_environment.IsDevelopment())
        {
            var bootstrapAdmins = _configuration.GetSection("BootstrapAdmins").Get<string[]>() ?? Array.Empty<string>();
            if (bootstrapAdmins.Any(admin => string.Equals(NormalizeUserName(admin), normalizedUserName, StringComparison.OrdinalIgnoreCase)))
            {
                await EnsureRoleAsync(user, AppConstants.Roles.Admin, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var employee = await GetEmployeeAsync(user.Codigo_Empleado, cancellationToken);
        var nombreCompleto = employee?.NombreCompleto ?? user.NombreCompleto;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, normalizedUserName),
            new("CodigoEmpleado", user.Codigo_Empleado ?? string.Empty),
            new("NombreCompleto", nombreCompleto ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new UserInfoDto(
            user.UserName,
            user.Codigo_Empleado,
            nombreCompleto,
            user.Instancia,
            roles,
            employee?.E_Mail,
            employee?.Departamento,
            employee is not null));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserInfoDto>> Me(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var employee = await GetEmployeeAsync(user.Codigo_Empleado, cancellationToken);
        var nombreCompleto = employee?.NombreCompleto ?? user.NombreCompleto;
        return new UserInfoDto(
            user.UserName,
            user.Codigo_Empleado,
            nombreCompleto,
            user.Instancia,
            roles,
            employee?.E_Mail,
            employee?.Departamento,
            employee is not null);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
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

        if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
        {
            user.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id, Role = role, User = user });
        }
    }

    private async Task<Employee?> GetEmployeeAsync(string? codigoEmpleado, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoEmpleado))
        {
            return null;
        }

        return await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo_Empleado == codigoEmpleado, cancellationToken);
    }

    private static string NormalizeUserName(string userName)
    {
        var trimmed = userName.Trim();
        var atIndex = trimmed.IndexOf('@');
        return atIndex > 0 ? trimmed[..atIndex] : trimmed;
    }

    private static string? TrimTo(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength);
    }
}
