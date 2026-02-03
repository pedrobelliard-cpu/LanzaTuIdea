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
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Usuario y contraseña son requeridos." });
        }

        var userName = request.UserName.Trim();
        var authenticated = await _adServiceClient.AuthenticateAsync(userName, request.Password, cancellationToken);
        if (!authenticated)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var adData = await _adServiceClient.GetUserDataAsync(userName, cancellationToken);
        if (adData is null)
        {
            return Unauthorized(new { message = "No fue posible obtener los datos del usuario." });
        }

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is not null && !user.IsActive)
        {
            return Forbid();
        }

        if (user is null)
        {
            user = new AppUser
            {
                UserName = userName,
                IsActive = true
            };
            _context.AppUsers.Add(user);
        }

        user.Codigo_Empleado = adData.CodigoEmpleado;
        user.NombreCompleto = adData.NombreCompleto;
        user.LastLoginAt = DateTime.UtcNow;

        await EnsureRoleAsync(user, "Ideador", cancellationToken);

        if (_environment.IsDevelopment())
        {
            var bootstrapAdmins = _configuration.GetSection("BootstrapAdmins").Get<string[]>() ?? Array.Empty<string>();
            if (bootstrapAdmins.Any(admin => string.Equals(admin, userName, StringComparison.OrdinalIgnoreCase)))
            {
                await EnsureRoleAsync(user, "Admin", cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new("CodigoEmpleado", user.Codigo_Empleado ?? string.Empty),
            new("NombreCompleto", user.NombreCompleto ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new UserInfoDto(user.UserName, user.Codigo_Empleado, user.NombreCompleto, roles));
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
        return new UserInfoDto(user.UserName, user.Codigo_Empleado, user.NombreCompleto, roles);
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
}
