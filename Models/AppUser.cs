namespace LanzaTuIdea.Api.Models;

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string? Codigo_Empleado { get; set; }
    public string? NombreCompleto { get; set; }
    public string? Instancia { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
    public List<Idea> Ideas { get; set; } = new();
}
