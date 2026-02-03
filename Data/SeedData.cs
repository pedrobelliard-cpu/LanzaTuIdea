using LanzaTuIdea.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LanzaTuIdea.Api.Data;

public static class SeedData
{
    private static readonly string[] DefaultRoles = ["Admin", "Ideador"];

    public static async Task InitializeAsync(AppDbContext context, IConfiguration configuration, IHostEnvironment environment)
    {
        var autoMigrate = environment.IsDevelopment() || configuration.GetValue<bool>("Database:AutoMigrate");
        if (autoMigrate)
        {
            await context.Database.MigrateAsync();
        }

        await SeedRolesAsync(context);

        if (environment.IsDevelopment())
        {
            await SeedBootstrapAdminsAsync(context, configuration);
        }

        await ImportEmployeesIfEmptyAsync(context, configuration);
    }

    private static async Task SeedRolesAsync(AppDbContext context)
    {
        var existingRoles = await context.Roles.Select(r => r.Name).ToListAsync();
        var missing = DefaultRoles.Except(existingRoles, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var role in missing)
        {
            context.Roles.Add(new Role { Name = role });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedBootstrapAdminsAsync(AppDbContext context, IConfiguration configuration)
    {
        var bootstrapAdmins = configuration.GetSection("BootstrapAdmins").Get<string[]>() ?? Array.Empty<string>();
        if (bootstrapAdmins.Length == 0)
        {
            return;
        }

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole is null)
        {
            return;
        }

        foreach (var userName in bootstrapAdmins)
        {
            var normalized = userName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var user = await context.AppUsers.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.UserName == normalized);
            if (user is null)
            {
                user = new AppUser { UserName = normalized, IsActive = true };
                context.AppUsers.Add(user);
                await context.SaveChangesAsync();
            }

            if (!user.UserRoles.Any(ur => ur.RoleId == adminRole.Id))
            {
                user.UserRoles.Add(new UserRole { RoleId = adminRole.Id, UserId = user.Id });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task ImportEmployeesIfEmptyAsync(AppDbContext context, IConfiguration configuration)
    {
        if (await context.Employees.AnyAsync())
        {
            return;
        }

        var root = configuration["Seed:EmployeesPath"] ?? Path.Combine(AppContext.BaseDirectory, "seed", "empleados.csv");
        var path = File.Exists(root)
            ? root
            : Path.Combine(Directory.GetCurrentDirectory(), "seed", "empleados.csv");

        if (!File.Exists(path))
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length <= 1)
        {
            return;
        }

        var employees = new List<Employee>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = ParseCsvLine(line);
            if (parts.Count < 7)
            {
                continue;
            }

            var employee = new Employee
            {
                Codigo_Empleado = parts[0].Trim(),
                Nombre = parts[1].Trim(),
                Apellido1 = parts[2].Trim(),
                Apellido2 = parts[3].Trim(),
                E_Mail = parts[4].Trim(),
                Departamento = parts[5].Trim(),
                Estatus = string.IsNullOrWhiteSpace(parts[6]) ? "A" : parts[6].Trim()
            };

            if (!string.IsNullOrWhiteSpace(employee.Codigo_Empleado))
            {
                employees.Add(employee);
            }
        }

        if (employees.Count > 0)
        {
            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
                continue;
            }

            current += ch;
        }

        result.Add(current);
        return result;
    }
}
