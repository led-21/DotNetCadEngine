using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DotNetCad.Infrastructure.MutuaConecta;

public sealed record MutuaConfig
{
    public string ApiBaseUrl { get; init; } = MutuaEndpointValidator.DefaultProductionUrl;
    public Guid? LastLinkedProjectId { get; init; }
    public string? LastLinkedProjectCode { get; init; }
    public string? LastUserEmail { get; init; }
}

/// <summary>
/// Securely stores session tokens using Windows DPAPI (ProtectedData) and manages local configuration.
/// Passwords are never stored on disk.
/// Includes cross-platform fallback for non-Windows environments (CI/Linux) without crashing.
/// </summary>
public sealed class MutuaCredentialStore
{
    private static readonly byte[] Entropy = "DotNetCadEngine.MutuaConecta.v1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _storageDirectory;
    private readonly string _sessionFilePath;
    private readonly string _configFilePath;

    // In-memory fallback for non-Windows systems if file encryption is unavailable
    private string? _inMemoryTokenFallback;

    public MutuaCredentialStore(string? customStorageDirectory = null)
    {
        _storageDirectory = customStorageDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotNetCadEngine");
        _sessionFilePath = Path.Combine(_storageDirectory, "mutua-session.dat");
        _configFilePath = Path.Combine(_storageDirectory, "mutua-config.json");
    }

    public void SaveSessionToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        try
        {
            Directory.CreateDirectory(_storageDirectory);

            if (OperatingSystem.IsWindows())
            {
                var tokenBytes = Encoding.UTF8.GetBytes(token);
                var encryptedBytes = ProtectedData.Protect(tokenBytes, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_sessionFilePath, encryptedBytes);
            }
            else
            {
                // Cross-platform fallback for non-Windows test/CI environments
                _inMemoryTokenFallback = token;
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
                File.WriteAllText(_sessionFilePath, base64);
            }
        }
        catch
        {
            _inMemoryTokenFallback = token;
        }
    }

    public string? LoadSessionToken()
    {
        if (_inMemoryTokenFallback != null)
        {
            return _inMemoryTokenFallback;
        }

        if (!File.Exists(_sessionFilePath))
        {
            return null;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var encryptedBytes = File.ReadAllBytes(_sessionFilePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            else
            {
                var base64 = File.ReadAllText(_sessionFilePath).Trim();
                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
        }
        catch
        {
            // If the token is corrupted or belongs to another user/machine, safely clear it
            ClearSession();
            return null;
        }
    }

    public void ClearSession()
    {
        _inMemoryTokenFallback = null;
        try
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
            }
        }
        catch
        {
            // Ignore file deletion errors during session cleanup
        }
    }

    public void SaveConfig(MutuaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        MutuaEndpointValidator.ValidateBaseUrl(config.ApiBaseUrl);

        Directory.CreateDirectory(_storageDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configFilePath, json);
    }

    public MutuaConfig LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            return new MutuaConfig();
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<MutuaConfig>(json);
            if (config != null && MutuaEndpointValidator.TryValidateBaseUrl(config.ApiBaseUrl, out _))
            {
                return config;
            }
        }
        catch
        {
            // Ignore deserialization errors and return defaults
        }

        return new MutuaConfig();
    }
}
