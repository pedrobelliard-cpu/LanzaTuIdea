using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AdServiceClient> _logger;

    public AdServiceClient(HttpClient httpClient, IOptions<AdServiceOptions> options, ILogger<AdServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUri(out var baseUri))
        {
            return false;
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreUsuario"] = userName,
            ["Password"] = password
        });

        try
        {
            using var response = await _httpClient.PostAsync(new Uri(baseUri, "Autenticacion"), content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);
            var value = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("boolean", StringComparison.OrdinalIgnoreCase))?.Value;
            return bool.TryParse(value, out var result) && result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Xml.XmlException)
        {
            _logger.LogWarning(ex, "Error autenticando con el servicio AD.");
            return false;
        }
    }

    public async Task<AdUserData?> GetUserDataAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUri(out var baseUri))
        {
            return null;
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreUsuario"] = userName
        });

        try
        {
            using var response = await _httpClient.PostAsync(new Uri(baseUri, "DatosUsuarioAD"), content, cancellationToken);
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Xml.XmlException)
        {
            _logger.LogWarning(ex, "Error consultando datos de usuario en el servicio AD.");
            return null;
        }
    }

    private bool TryGetBaseUri(out Uri baseUri)
    {
        baseUri = default!;
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("AdService BaseUrl no está configurado.");
            return false;
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var parsed))
        {
            _logger.LogWarning("AdService BaseUrl no es un URI válido: {BaseUrl}", _options.BaseUrl);
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            _logger.LogWarning("AdService BaseUrl debe usar http/https.");
            return false;
        }

        baseUri = parsed;
        return true;
    }
}
