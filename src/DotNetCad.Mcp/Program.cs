using DotNetCad.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<CadMcpBridgeClient>();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CadTools>()
    .WithTools<MutuaConectaTools>();
await builder.Build().RunAsync();
