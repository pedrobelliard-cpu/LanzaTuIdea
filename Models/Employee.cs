using System.Linq;

namespace LanzaTuIdea.Api.Models;

public class Employee
{
    public string Codigo_Empleado { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Apellido1 { get; set; } = "";
    public string Apellido2 { get; set; } = "";
    public string E_Mail { get; set; } = "";
    public string Departamento { get; set; } = "";
    public string Estatus { get; set; } = "A";

    public string NombreCompleto => string.Join(" ", new[] { Nombre, Apellido1, Apellido2 }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
