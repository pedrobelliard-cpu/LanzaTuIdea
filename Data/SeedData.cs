using LanzaTuIdea.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LanzaTuIdea.Api.Data;

public static class SeedData
{
    private static readonly string[] DefaultRoles = ["Admin", "Ideador"];

    public static async Task InitializeAsync(AppDbContext context, IConfiguration configuration, IHostEnvironment environment)
    {
        Console.WriteLine("--> [SeedData] Iniciando inicialización de datos...");
        
        var autoMigrate = environment.IsDevelopment() || configuration.GetValue<bool>("Database:AutoMigrate");
        if (autoMigrate)
        {
            Console.WriteLine("--> [SeedData] Aplicando migraciones pendientes...");
            await context.Database.MigrateAsync();
        }

        await SeedRolesAsync(context);

        if (environment.IsDevelopment())
        {
            await SeedBootstrapAdminsAsync(context, configuration);
        }

        await ImportEmployeesIfEmptyAsync(context, configuration);
        
        Console.WriteLine("--> [SeedData] Proceso finalizado.");
    }

    private static async Task SeedRolesAsync(AppDbContext context)
    {
        var existingRoles = await context.Roles.Select(r => r.Name).ToListAsync();
        var missing = DefaultRoles.Except(existingRoles, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0) return;

        foreach (var role in missing)
        {
            context.Roles.Add(new Role { Name = role });
        }
        await context.SaveChangesAsync();
        Console.WriteLine($"--> [SeedData] Roles creados: {string.Join(", ", missing)}");
    }

    private static async Task SeedBootstrapAdminsAsync(AppDbContext context, IConfiguration configuration)
    {
        var bootstrapAdmins = configuration.GetSection("BootstrapAdmins").Get<string[]>() ?? Array.Empty<string>();
        if (bootstrapAdmins.Length == 0) return;

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole is null) return;

        foreach (var userName in bootstrapAdmins)
        {
            var normalized = userName.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) continue;

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
                Console.WriteLine($"--> [SeedData] Admin bootstrap asignado a: {normalized}");
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task ImportEmployeesIfEmptyAsync(AppDbContext context, IConfiguration configuration)
    {
        if (await context.Employees.AnyAsync())
        {
            Console.WriteLine("--> [SeedData] La tabla Employees ya tiene datos. Se omite la carga.");
            return;
        }

        var root = configuration["Seed:EmployeesPath"] ?? Path.Combine(AppContext.BaseDirectory, "seed", "empleados.csv");
        var path = File.Exists(root) ? root : Path.Combine(Directory.GetCurrentDirectory(), "seed", "empleados.csv");

        Console.WriteLine($"--> [SeedData] Buscando archivo en: {path}");

        if (!File.Exists(path))
        {
            Console.WriteLine("--> [SeedData] ADVERTENCIA: No se encontró el archivo CSV.");
            return;
        }

        // Leer con Encoding por defecto (UTF8)
        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length <= 1)
        {
            Console.WriteLine("--> [SeedData] El archivo CSV está vacío o solo tiene cabecera.");
            return;
        }

        // DETECCIÓN INTELIGENTE DE DELIMITADOR
        var header = lines[0];
        char delimiter = header.Contains(';') ? ';' : ',';
        Console.WriteLine($"--> [SeedData] Delimitador detectado: ' {delimiter} '");

        var employees = new List<Employee>();
        int skipped = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Intento 1: Parser que respeta comillas
            var parts = ParseCsvLine(line, delimiter);

            // Intento 2 (Fallback): Si el parser devuelve 1 sola parte pero la línea tiene el delimitador,
            // usamos Split simple. Esto arregla casos donde el formato es sencillo pero el parser falla.
            if (parts.Count <= 1 && line.Contains(delimiter))
            {
                parts = line.Split(delimiter).Select(p => p.Trim()).ToList();
            }

            // Validación de columnas
            if (parts.Count < 5) 
            {
                skipped++;
                // LOG DE ERROR PARA VER QUÉ ESTÁ PASANDO REALMENTE
                Console.WriteLine($"--> [ERROR FORMATO] Línea omitida. Se esperaban 5+ columnas, se encontraron {parts.Count}.");
                Console.WriteLine($"    Contenido: '{line}'");
                continue;
            }

            try 
            {
                var employee = new Employee
                {
                    Codigo_Empleado = Truncate(parts[0], 20),
                    Nombre          = parts.Count > 1 ? Truncate(parts[1], 100) : "",
                    Apellido1       = parts.Count > 2 ? Truncate(parts[2], 100) : "",
                    Apellido2       = parts.Count > 3 ? Truncate(parts[3], 100) : "",
                    E_Mail          = parts.Count > 4 ? Truncate(parts[4], 200) : "",
                    Departamento    = parts.Count > 5 ? Truncate(parts[5], 200) : "", 
                    Estatus         = (parts.Count > 6 && !string.IsNullOrWhiteSpace(parts[6])) 
                                      ? Truncate(parts[6], 5) 
                                      : "A"
                };

                if (!string.IsNullOrWhiteSpace(employee.Codigo_Empleado))
                {
                    employees.Add(employee);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> [ERROR MAPPING] Error al procesar línea: {line}. Error: {ex.Message}");
                skipped++;
            }
        }

        if (employees.Count > 0)
        {
            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();
            Console.WriteLine($"--> [SeedData] ÉXITO: Se cargaron {employees.Count} empleados.");
        }
        
        if (skipped > 0) Console.WriteLine($"--> [SeedData] Se omitieron {skipped} líneas por errores.");
    }

    // Parser manual que respeta comillas y acepta delimitador dinámico
    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue; // Omitir las comillas en el valor final
            }

            if (ch == delimiter && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
                continue;
            }

            current += ch;
        }

        result.Add(current.Trim());
        return result;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var val = value.Trim();
        return val.Length <= maxLength ? val.Substring(0, maxLength) : val;
    }
}
