using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCad.Infrastructure.MutuaConecta;
using Xunit;

namespace DotNetCad.Core.Tests;

public sealed class MutuaConectaClientTests
{
    [Theory]
    [InlineData("https://mutua.grifocorretora.com")]
    [InlineData("http://localhost:5180")]
    [InlineData("http://127.0.0.1:5180")]
    [InlineData("https://localhost:7180")]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    public void EndpointValidator_PermittedEndpoints_Succeed(string url)
    {
        bool isValid = MutuaEndpointValidator.TryValidateBaseUrl(url, out var error);
        Assert.True(isValid, $"Expected valid URL for '{url}', but got error: {error}");
        Assert.Null(error);

        var normalized = MutuaEndpointValidator.NormalizeBaseUrl(url);
        Assert.NotNull(normalized);
        Assert.True(normalized.ToString().EndsWith('/'));
    }

    [Theory]
    [InlineData("http://mutua.grifocorretora.com")]
    [InlineData("http://api.mutuaconecta.com")]
    [InlineData("http://192.168.1.50:5180")]
    [InlineData("http://example.com")]
    [InlineData("ftp://mutua.grifocorretora.com")]
    [InlineData("not-a-valid-url")]
    [InlineData("")]
    [InlineData(null)]
    public void EndpointValidator_RemoteHttpAndInvalidUrls_AreBlocked(string? url)
    {
        bool isValid = MutuaEndpointValidator.TryValidateBaseUrl(url, out var error);
        Assert.False(isValid, $"Expected '{url}' to be rejected, but it was accepted.");
        Assert.NotNull(error);

        Assert.Throws<ArgumentException>(() => MutuaEndpointValidator.ValidateBaseUrl(url));
    }

    [Fact]
    public void CredentialStore_SaveLoadAndClear_WorksCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DotNetCad_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new MutuaCredentialStore(tempDir);

            // Initially empty
            Assert.Null(store.LoadSessionToken());

            // Save token
            const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.sample_payload.signature";
            store.SaveSessionToken(token);

            var loaded = store.LoadSessionToken();
            Assert.Equal(token, loaded);

            // Save config
            var testProjectId = Guid.NewGuid();
            var config = new MutuaConfig
            {
                ApiBaseUrl = "https://mutua.grifocorretora.com",
                LastLinkedProjectId = testProjectId,
                LastLinkedProjectCode = "PRJ-2026-001",
                LastUserEmail = "carlos.mendes@demo.mutuaconecta.local"
            };
            store.SaveConfig(config);

            var loadedConfig = store.LoadConfig();
            Assert.Equal("https://mutua.grifocorretora.com", loadedConfig.ApiBaseUrl);
            Assert.Equal(testProjectId, loadedConfig.LastLinkedProjectId);
            Assert.Equal("PRJ-2026-001", loadedConfig.LastLinkedProjectCode);
            Assert.Equal("carlos.mendes@demo.mutuaconecta.local", loadedConfig.LastUserEmail);

            // Clear session
            store.ClearSession();
            Assert.Null(store.LoadSessionToken());

            // Config remains intact after session clear
            var configAfterSessionClear = store.LoadConfig();
            Assert.Equal("PRJ-2026-001", configAfterSessionClear.LastLinkedProjectCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ApiClient_WhenOffline_FailsGracefullyWithoutThrowing()
    {
        // Using an offline handler that throws HttpRequestException instantly
        var offlineHandler = new OfflineSimulatingHandler();
        var client = new MutuaConectaApiClient(new HttpClient(offlineHandler), baseUrl: "http://localhost:5180");

        var status = await client.CheckHealthAsync();
        Assert.False(status.IsHealthy);
        Assert.NotNull(status.ErrorMessage);

        var authResult = await client.LoginAsync("user@demo.local", "wrongpassword");
        Assert.False(authResult.IsSuccess);
        Assert.NotNull(authResult.ErrorMessage);

        var projects = await client.GetMyProjectsAsync();
        Assert.Empty(projects);

        var details = await client.GetProjectDetailsAsync(Guid.NewGuid());
        Assert.Null(details);

        var matches = await client.GetRecommendedSpecialistsAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Empty(matches);

        var syncResult = await client.SyncCadDocumentMetadataAsync(Guid.NewGuid(), new MutuaCadDocumentSyncRequest(
            ProjectDisciplineId: null,
            Title: "Test",
            FileName: "Test.cad",
            Format: "CadNative",
            Sha256Hash: "abc",
            FileSizeBytes: 100,
            CadMetadataJson: null,
            ContentPreviewSvg: null
        ));
        Assert.False(syncResult.IsSuccess);
        Assert.NotNull(syncResult.ErrorMessage);
    }

    private sealed class OfflineSimulatingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
        }
    }

    [Fact]
    public async Task ApiClient_MockedSuccessResponses_ParseCorrectly()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new MutuaConectaApiClient(httpClient, "http://localhost:5180");

        // 1. Health check
        handler.SetResponse("/api/health/ready", HttpStatusCode.OK, """{"status":"Healthy","database":"Connected"}""");
        var health = await client.CheckHealthAsync();
        Assert.True(health.IsHealthy);
        Assert.Equal("Connected", health.DatabaseStatus);

        // 2. Login
        handler.SetResponse("/api/v1/auth/login", HttpStatusCode.OK, """
        {
            "accessToken": "mocked.jwt.token",
            "expiresAtUtc": "2026-10-28T12:00:00Z",
            "user": {
                "userId": "11111111-1111-1111-1111-111111111101",
                "profileId": "22222222-2222-2222-2222-222222222201",
                "email": "carlos.mendes@demo.mutuaconecta.local",
                "fullName": "Eng. Carlos Eduardo Mendes",
                "title": "Engenheiro Agrônomo",
                "primarySpecialtyCode": "ENG-AGR",
                "stateUf": "MS"
            }
        }
        """);
        var login = await client.LoginAsync("carlos.mendes@demo.mutuaconecta.local", "Demo@Mutua2026!");
        Assert.True(login.IsSuccess);
        Assert.Equal("mocked.jwt.token", login.AccessToken);
        Assert.Equal("mocked.jwt.token", client.SessionToken);
        Assert.Equal("Eng. Carlos Eduardo Mendes", login.User?.FullName);

        // 3. Sync CAD
        handler.SetResponse("/api/v1/projects/33333333-3333-3333-3333-333333333333/documents/cad-sync", HttpStatusCode.Created, """
        {
            "id": "77777777-7777-7777-7777-777777777777",
            "versionNumber": 2,
            "fileName": "UnidadeBeneficiamentoSilos.cad"
        }
        """);
        var sync = await client.SyncCadDocumentMetadataAsync(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            new MutuaCadDocumentSyncRequest(
                ProjectDisciplineId: null,
                Title: "Implantação Geral",
                FileName: "UnidadeBeneficiamentoSilos.cad",
                Format: "CadNative",
                Sha256Hash: "deadbeef",
                FileSizeBytes: 1234,
                CadMetadataJson: null,
                ContentPreviewSvg: "<svg></svg>"
            ));
        Assert.True(sync.IsSuccess);
        Assert.Equal(Guid.Parse("77777777-7777-7777-7777-777777777777"), sync.DocumentId);
        Assert.Equal(2, sync.VersionNumber);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode status, string body)> _responses = new(StringComparer.OrdinalIgnoreCase);

        public void SetResponse(string path, HttpStatusCode status, string body)
        {
            _responses[path] = (status, body);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (_responses.TryGetValue(path, out var resp))
            {
                var responseMessage = new HttpResponseMessage(resp.status)
                {
                    Content = new StringContent(resp.body, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(responseMessage);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"Not found\"}", Encoding.UTF8, "application/json")
            });
        }
    }
}
