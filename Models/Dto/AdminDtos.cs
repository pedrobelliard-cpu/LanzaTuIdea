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
    bool IsActive,
    IReadOnlyList<string> Roles
);

public record UpdateRolesRequest(IReadOnlyList<string> Roles);

public record UpdateActiveRequest(bool IsActive);

public record EmployeeLookupDto(
    string CodigoEmpleado,
    string? NombreCompleto,
    string? Email,
    string? Departamento
);
