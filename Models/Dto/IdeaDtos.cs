namespace LanzaTuIdea.Api.Models.Dto;

public record IdeaCreateRequest(
    string Descripcion,
    string Detalle,
    string? NombreCompleto,
    string? Email,
    string? Departamento
);

public record IdeaSummaryDto(
    int Id,
    DateTime CreatedAt,
    string Descripcion,
    string Status
);

public record IdeaAdminSummaryDto(
    int Id,
    DateTime CreatedAt,
    string Descripcion,
    string Status,
    string CodigoEmpleado,
    string? NombreCompleto,
    string? Email,
    string? Departamento,
    string? Clasificacion
);

public record IdeaDetailDto(
    int Id,
    DateTime CreatedAt,
    string Descripcion,
    string Detalle,
    string Status,
    string? Clasificacion,
    string? Via,
    string? AdminComment,
    string CodigoEmpleado,
    string? NombreCompleto,
    IReadOnlyList<IdeaHistoryDto> History
);

public record IdeaHistoryDto(
    DateTime ChangedAt,
    string ChangedBy,
    string ChangeType,
    string? Notes
);

public record IdeaReviewRequest(
    string Status,
    string? Clasificacion,
    string? AdminComment
);

public record IdeaManualRequest(
    string CodigoEmpleado,
    string Descripcion,
    string Detalle,
    string? Via,
    string? AdminComment,
    string? NombreCompleto,
    string? Email,
    string? Departamento
);
