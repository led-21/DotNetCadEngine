using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DotNetCad.Infrastructure.Mcp;

namespace DotNetCad.Mcp;

public sealed class CadMcpBridgeClient
{
    public Task<CadMcpResponse> SendAsync(string method, string? command = null) =>
        SendAsync(new CadMcpRequest(method, command));

    public async Task<CadMcpResponse> SendAsync(CadMcpRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var pipe = new NamedPipeClientStream(
            ".", CadMcpProtocol.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            var line = await reader.ReadLineAsync(timeout.Token);
            return line == null
                ? new CadMcpResponse(false, "The CAD application closed the connection.", Code: "PIPE_CLOSED")
                : JsonSerializer.Deserialize<CadMcpResponse>(line)
                    ?? new CadMcpResponse(false, "Invalid response from the CAD application.", Code: "INVALID_RESPONSE");
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            return new CadMcpResponse(false, "Open DotNetCad.Demo on this Windows account before using MCP. " + ex.Message, Code: "PIPE_UNAVAILABLE");
        }
    }
}
