namespace LanzaTuIdea.Api.Models.Dto;

public record DashboardDto(
    int Total,
    int Pendientes,
    int Revisadas,
    int UsuariosActivos,
    IReadOnlyList<CountByLabelDto> PorStatus,
    IReadOnlyList<CountByLabelDto> PorClasificacion
);

public record CountByLabelDto(string Label, int Count);

public record UserSummaryDto(
    string UserName,
    string? CodigoEmpleado,
    string? NombreCompleto,
    string? Instancia,
    bool IsActive,
    IReadOnlyList<string> Roles
);

public record UpdateRolesRequest(IReadOnlyList<string> Roles);

public record UpdateActiveRequest(bool IsActive);

public record CreateUserRequest(string UserName, string? Role, string? Instancia);

public record UpdateUserInstanceRequest(string? Instancia);

public record EmployeeLookupDto(
    string CodigoEmpleado,
    string? NombreCompleto,
    string? Email,
    string? Departamento
);
