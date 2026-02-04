namespace LanzaTuIdea.Api.Models.Dto;

public record DashboardDto(
    int Total,
    int Pendientes,
    int Revisadas,
    int UsuariosActivos,
    IReadOnlyList<CountByLabelDto> PorStatus,
    IReadOnlyList<CountByLabelDto> PorClasificacion,
    IReadOnlyList<CountByLabelDto> PorVia,
    IReadOnlyList<CountByLabelDto> PorInstancia,
    IReadOnlyList<CountByLabelDto> PorDepartamento
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

public record TimelineFilterRequest(
    string Periodo,
    IReadOnlyList<string>? Status,
    IReadOnlyList<string>? Vias,
    IReadOnlyList<string>? Instancias,
    IReadOnlyList<string>? Departamentos
);

public record TimelineResponse(
    IReadOnlyList<TimePointDto> Puntos,
    int TotalFiltrado
);

public record TimePointDto(DateTime Fecha, int Cantidad);
