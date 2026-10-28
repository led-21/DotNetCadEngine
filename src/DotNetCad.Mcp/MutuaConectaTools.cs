using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DotNetCad.Mcp;

[McpServerToolType]
public sealed class MutuaConectaTools(CadMcpBridgeClient bridge)
{
    [McpServerTool, Description("READ-ONLY: Returns details of the Mútua Conecta multidisciplinary project currently linked to the desktop CAD drawing, including disciplines, members, and open requirements.")]
    public async Task<string> MutuaGetLinkedProject()
    {
        var response = await bridge.SendAsync("mutua_get_linked_project");
        return response.Success
            ? response.Content ?? response.Message
            : $"ERROR: {response.Message}";
    }

    [McpServerTool, Description("READ-ONLY: Analyzes the layers and technical entities of the active CAD drawing in DotNetCad.Demo, cross-referencing with linked project disciplines to detect missing engineering specializations.")]
    public async Task<string> MutuaInspectCadDisciplines()
    {
        var response = await bridge.SendAsync("mutua_inspect_cad_disciplines");
        return response.Success
            ? response.Content ?? response.Message
            : $"ERROR: {response.Message}";
    }

    [McpServerTool, Description("READ-ONLY: Queries the deterministic matching engine on Mútua Conecta for recommended specialists for the active project requirement (e.g. electrical substation and SPDA).")]
    public async Task<string> MutuaFindSpecialists(
        [Description("Optional project requirement GUID string")] string? requirementId = null)
    {
        var response = await bridge.SendAsync("mutua_find_specialists", requirementId);
        return response.Success
            ? response.Content ?? response.Message
            : $"ERROR: {response.Message}";
    }

    [McpServerTool, Description("STAGE/DRAFT (Safe): Prepares a collaboration invitation draft in the DotNetCad.Demo desktop UI for explicit human review and confirmation. NEVER sends invitations without explicit human click in the UI.")]
    public async Task<string> MutuaPrepareCollaborationDraft(
        [Description("Recipient specialist display name (e.g. 'Eng. Helena Vasconcelos')")] string recipientName,
        [Description("Invitation message explaining the technical requirement and project context")] string message,
        [Description("Proposed honorarium in BRL (e.g. 14500.00)")] double proposedHonorariumBrl,
        [Description("Optional specialist profile GUID string")] string? recipientProfileId = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            recipientName,
            message,
            proposedHonorariumBrl = (decimal)proposedHonorariumBrl,
            recipientProfileId
        });
        var response = await bridge.SendAsync("mutua_prepare_collaboration_draft", payload);
        return response.Success
            ? response.Message + (response.Content != null ? "\n" + response.Content : string.Empty)
            : $"ERROR: {response.Message}";
    }
}
