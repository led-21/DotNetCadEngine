using DotNetCad.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<CadMcpBridgeClient>();
builder.Services.AddMcpServer(options => options.ServerInstructions =
    "Before engineering drafting, call list_engineering_skills and read_engineering_skill for cad-preparar-projeto, " +
    "then relevant discipline skills and nbr-consulta-e-evidencias. Inspect drawing and units before changes. " +
    "Skills are workflow guidance, not normative text or certification. Verify applicable ABNT editions and requirements " +
    "using authorized sources; report unverified items. This guidance does not enforce a server-side mutation gate.")
    .WithStdioServerTransport()
    .WithTools<CadTools>()
    .WithTools<EngineeringSkillTools>()
    .WithTools<MutuaConectaTools>();
await builder.Build().RunAsync();
