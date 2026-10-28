using System.Diagnostics.CodeAnalysis;

namespace DotNetCad.Infrastructure.MutuaConecta;

/// <summary>
/// Validates and normalizes base URLs for the Mútua Conecta API.
/// Enforces TLS (HTTPS) for all remote hosts to prevent SSRF and token leakage over unencrypted channels.
/// HTTP is permitted strictly for local development (localhost / 127.0.0.1 / loopback).
/// </summary>
public static class MutuaEndpointValidator
{
    public const string DefaultProductionUrl = "https://mutua.grifocorretora.com";
    public const string DefaultLocalDevelopmentUrl = "http://localhost:5180";

    public static bool TryValidateBaseUrl(string? url, [NotNullWhen(false)] out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            errorMessage = "A URL base da API do Mútua Conecta não pode ser vazia.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = "A URL informada não é válida ou não possui esquema absoluto.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = $"Esquema '{uri.Scheme}' não suportado. Apenas HTTP e HTTPS são aceitos.";
            return false;
        }

        bool isLocal = IsLocalHost(uri);

        if (!isLocal && uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = "Conexões com servidores remotos exigem obrigatoriamente HTTPS (TLS) por motivos de segurança.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static void ValidateBaseUrl(string? url)
    {
        if (!TryValidateBaseUrl(url, out var error))
        {
            throw new ArgumentException(error, nameof(url));
        }
    }

    public static Uri NormalizeBaseUrl(string url)
    {
        ValidateBaseUrl(url);
        var trimmed = url.Trim();
        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }
        return new Uri(trimmed, UriKind.Absolute);
    }

    public static bool IsLocalHost(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsLoopback) return true;

        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
    }
}
