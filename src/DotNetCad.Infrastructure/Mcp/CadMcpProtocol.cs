namespace DotNetCad.Infrastructure.Mcp;

public static class CadMcpProtocol
{
    public static string PipeName => $"DotNetCadEngine.Mcp.v1.{Environment.UserName}";
}

public sealed record CadMcpRequest(string Method, string? Command = null);

public sealed record CadMcpResponse(bool Success, string Message, string? Content = null);
