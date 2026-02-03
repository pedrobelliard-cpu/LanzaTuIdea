using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using System.Linq;

namespace LanzaTuIdea.Api.Services;

public interface IAdServiceClient
{
    Task<bool> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task<AdUserData?> GetUserDataAsync(string userName, CancellationToken cancellationToken = default);
}

public record AdUserData(string CodigoEmpleado, string NombreCompleto);

public class AdServiceOptions
{
    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}

public class AdServiceClient : IAdServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AdServiceOptions _options;

    public AdServiceClient(HttpClient httpClient, IOptions<AdServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreUsuario"] = userName,
            ["Password"] = password
        });

        using var response = await _httpClient.PostAsync(new Uri(new Uri(_options.BaseUrl), "Autenticacion"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = XDocument.Parse(xml);
        var value = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("boolean", StringComparison.OrdinalIgnoreCase))?.Value;
        return bool.TryParse(value, out var result) && result;
    }

    public async Task<AdUserData?> GetUserDataAsync(string userName, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreUsuario"] = userName
        });

        using var response = await _httpClient.PostAsync(new Uri(new Uri(_options.BaseUrl), "DatosUsuarioAD"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = XDocument.Parse(xml);
        var codigo = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("CodigoEmpleado", StringComparison.OrdinalIgnoreCase))?.Value;
        var nombre = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("NombreCompleto", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        return new AdUserData(codigo.Trim(), nombre?.Trim() ?? string.Empty);
    }
}
