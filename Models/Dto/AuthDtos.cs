namespace LanzaTuIdea.Api.Models.Dto;

public record LoginRequest(string UserName, string Password);

public record UserInfoDto(
    string UserName,
    string? CodigoEmpleado,
    string? NombreCompleto,
    IReadOnlyList<string> Roles
);
