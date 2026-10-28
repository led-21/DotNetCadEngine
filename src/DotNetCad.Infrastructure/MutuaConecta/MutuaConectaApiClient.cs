using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DotNetCad.Infrastructure.MutuaConecta;

/// <summary>
/// Resilient, decoupled HTTP client for the Mútua Conecta REST API.
/// Defaults to an 8-second timeout and gracefully falls back when offline,
/// allowing CAD operations to proceed uninterrupted without network connectivity.
/// </summary>
public sealed class MutuaConectaApiClient : IMutuaConectaApiClient, IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private string _baseUrl;

    public string BaseUrl => _baseUrl;
    public string? SessionToken { get; set; }

    public MutuaConectaApiClient(HttpClient? httpClient = null, string baseUrl = MutuaEndpointValidator.DefaultProductionUrl)
    {
        MutuaEndpointValidator.ValidateBaseUrl(baseUrl);
        _baseUrl = MutuaEndpointValidator.NormalizeBaseUrl(baseUrl).ToString().TrimEnd('/');

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = DefaultTimeout };
            _ownsHttpClient = true;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public void SetBaseUrl(string url)
    {
        MutuaEndpointValidator.ValidateBaseUrl(url);
        _baseUrl = MutuaEndpointValidator.NormalizeBaseUrl(url).ToString().TrimEnd('/');
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, HttpContent? content = null)
    {
        var requestUri = new Uri($"{_baseUrl}/{relativeUri.TrimStart('/')}");
        var request = new HttpRequestMessage(method, requestUri);

        if (!string.IsNullOrWhiteSpace(SessionToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
        }

        if (content != null)
        {
            request.Content = content;
        }

        return request;
    }

    private async Task<HttpResponseMessage?> SendAsyncSafe(HttpRequestMessage request, CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(DefaultTimeout);

        try
        {
            return await _httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<MutuaConnectionStatus> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "api/health/ready");
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null)
            {
                return new MutuaConnectionStatus(false, ErrorMessage: "API Mútua Conecta indisponível — operando em modo offline.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new MutuaConnectionStatus(false, ErrorMessage: $"API retornou status HTTP {(int)response.StatusCode}.");
            }

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct).ConfigureAwait(false);
            var status = content.TryGetProperty("status", out var s) ? s.GetString() : "Healthy";
            var database = content.TryGetProperty("database", out var d) ? d.GetString() : null;

            return new MutuaConnectionStatus(
                IsHealthy: string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase),
                DatabaseStatus: database,
                Service: "MutuaConecta.Api");
        }
        catch (Exception ex)
        {
            return new MutuaConnectionStatus(false, ErrorMessage: $"Falha ao consultar saúde da API: {ex.Message}");
        }
    }

    public async Task<MutuaAuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonContent.Create(new { email, password });
            using var req = CreateRequest(HttpMethod.Post, "api/v1/auth/login", payload);
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null)
            {
                return new MutuaAuthResult(false, ErrorMessage: "API Mútua Conecta indisponível no momento — verifique a rede ou opere offline.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new MutuaAuthResult(false, ErrorMessage: $"Falha na autenticação (HTTP {(int)response.StatusCode}). Verifique suas credenciais.");
            }

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct).ConfigureAwait(false);
            var accessToken = doc.GetProperty("accessToken").GetString();
            DateTimeOffset? expiresAt = doc.TryGetProperty("expiresAtUtc", out var exp) && exp.TryGetDateTimeOffset(out var expDate)
                ? expDate : null;

            MutuaUserSummaryDto? user = null;
            if (doc.TryGetProperty("user", out var u))
            {
                user = JsonSerializer.Deserialize<MutuaUserSummaryDto>(u.GetRawText(), JsonOptions);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                SessionToken = accessToken;
                return new MutuaAuthResult(true, accessToken, expiresAt, user);
            }

            return new MutuaAuthResult(false, ErrorMessage: "Token JWT não foi retornado pelo servidor.");
        }
        catch (Exception ex)
        {
            return new MutuaAuthResult(false, ErrorMessage: $"Erro ao autenticar: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<MutuaProjectSummaryDto>> GetMyProjectsAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "api/v1/projects");
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null || !response.IsSuccessStatusCode)
            {
                return [];
            }

            var list = await response.Content.ReadFromJsonAsync<List<MutuaProjectSummaryDto>>(JsonOptions, ct).ConfigureAwait(false);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<MutuaProjectDetailDto?> GetProjectDetailsAsync(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, $"api/v1/projects/{projectId}");
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MutuaProjectDetailDto>(JsonOptions, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<MutuaProfessionalMatchDto>> GetRecommendedSpecialistsAsync(
        Guid projectId, Guid requirementId, CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, $"api/v1/projects/{projectId}/requirements/{requirementId}/matches");
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null || !response.IsSuccessStatusCode)
            {
                return [];
            }

            var matches = await response.Content.ReadFromJsonAsync<List<MutuaProfessionalMatchDto>>(JsonOptions, ct).ConfigureAwait(false);
            return matches ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<MutuaInvitationDto?> SendCollaborationInvitationAsync(
        Guid projectId, MutuaCreateInvitationRequest request, CancellationToken ct = default)
    {
        try
        {
            var content = JsonContent.Create(request);
            using var req = CreateRequest(HttpMethod.Post, $"api/v1/projects/{projectId}/invitations", content);
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MutuaInvitationDto>(JsonOptions, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<MutuaDocumentSyncResult> SyncCadDocumentMetadataAsync(
        Guid projectId, MutuaCadDocumentSyncRequest request, CancellationToken ct = default)
    {
        try
        {
            var content = JsonContent.Create(request);
            using var req = CreateRequest(HttpMethod.Post, $"api/v1/projects/{projectId}/documents/cad-sync", content);
            var response = await SendAsyncSafe(req, ct).ConfigureAwait(false);

            if (response == null)
            {
                return new MutuaDocumentSyncResult(false, ErrorMessage: "Não foi possível conectar à API do Mútua Conecta. Verifique sua conexão.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new MutuaDocumentSyncResult(false, ErrorMessage: $"Falha ao sincronizar documento (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct).ConfigureAwait(false);
            Guid? docId = doc.TryGetProperty("id", out var idProp) && idProp.TryGetGuid(out var g) ? g : null;
            int? version = doc.TryGetProperty("versionNumber", out var vProp) && vProp.TryGetInt32(out var v) ? v : null;

            return new MutuaDocumentSyncResult(
                IsSuccess: true,
                DocumentId: docId,
                VersionNumber: version,
                Message: "Documento CAD sincronizado com sucesso com o Mútua Conecta.");
        }
        catch (Exception ex)
        {
            return new MutuaDocumentSyncResult(false, ErrorMessage: $"Erro na sincronização CAD: {ex.Message}");
        }
    }
}
