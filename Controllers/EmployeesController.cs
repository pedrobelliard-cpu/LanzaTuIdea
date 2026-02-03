using LanzaTuIdea.Api.Data;
using LanzaTuIdea.Api.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanzaTuIdea.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _context;

    public EmployeesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeLookupDto>> Me(CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(user.Codigo_Empleado))
        {
            return new EmployeeLookupDto(string.Empty, user.NombreCompleto, null, null);
        }

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Codigo_Empleado == user.Codigo_Empleado, cancellationToken);
        if (employee is null)
        {
            return new EmployeeLookupDto(user.Codigo_Empleado, user.NombreCompleto, null, null);
        }

        return new EmployeeLookupDto(employee.Codigo_Empleado, employee.NombreCompleto, employee.E_Mail, employee.Departamento);
    }
}
