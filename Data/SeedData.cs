using LanzaTuIdea.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text; // Necesario para Encoding

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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Employees");

        var root = configuration["Seed:EmployeesPath"] ?? Path.Combine(AppContext.BaseDirectory, "seed", "empleados.csv");
        var path = File.Exists(root) ? root : Path.Combine(Directory.GetCurrentDirectory(), "seed", "empleados.csv");

        Console.WriteLine($"--> [SeedData] Buscando archivo en: {path}");

        if (!File.Exists(path))
        {
            Console.WriteLine("--> [SeedData] ADVERTENCIA: No se encontró el archivo CSV.");
            return;
        }

        // CORRECCIÓN DE ENCODING: Usamos Latin1 para soportar archivos de Excel/Windows en español
        var lines = await File.ReadAllLinesAsync(path, Encoding.Latin1);
        
        if (lines.Length <= 1)
        {
            Console.WriteLine("--> [SeedData] El archivo CSV está vacío o solo tiene cabecera.");
            return;
        }

        var header = lines[0];
        char delimiter = header.Contains(';') ? ';' : ',';
        Console.WriteLine($"--> [SeedData] Delimitador detectado: ' {delimiter} '");

        var employees = new List<Employee>();
        int skipped = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line, delimiter);

            // Fallback simple
            if (parts.Count <= 1 && line.Contains(delimiter))
            {
                parts = line.Split(delimiter).Select(p => p.Trim()).ToList();
            }

            if (parts.Count < 5) 
            {
                skipped++;
                Console.WriteLine($"--> [ERROR FORMATO] Línea omitida. Se encontraron {parts.Count} columnas.");
                continue;
            }

            try 
            {
                // Función auxiliar interna para limpiar comillas y espacios
                string Clean(string? value) => value?.Trim().Trim('"').Trim() ?? "";

                var employee = new Employee
                {
                    Codigo_Empleado = Truncate(Clean(parts[0]), 20),
                    Nombre          = parts.Count > 1 ? Truncate(Clean(parts[1]), 100) : "",
                    Apellido1       = parts.Count > 2 ? Truncate(Clean(parts[2]), 100) : "",
                    Apellido2       = parts.Count > 3 ? Truncate(Clean(parts[3]), 100) : "",
                    E_Mail          = parts.Count > 4 ? Truncate(Clean(parts[4]), 200) : "",
                    Departamento    = parts.Count > 5 ? Truncate(Clean(parts[5]), 200) : "", 
                    Estatus         = (parts.Count > 6 && !string.IsNullOrWhiteSpace(parts[6])) 
                                      ? Truncate(Clean(parts[6]), 5) 
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
                continue;
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

    // CORRECCIÓN DEL BUG LÓGICO AQUÍ
    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var val = value.Trim();
        // Si la longitud es menor o igual al máximo, devolvemos el valor COMPLETO.
        // Solo si es mayor, cortamos.
        return val.Length <= maxLength ? val : val.Substring(0, maxLength);
    }
}
