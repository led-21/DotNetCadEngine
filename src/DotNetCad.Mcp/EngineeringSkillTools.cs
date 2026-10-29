using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DotNetCad.Mcp;

/// <summary>Read-only access to the engineering guidance shipped with this MCP build.</summary>
[McpServerToolType]
public sealed class EngineeringSkillTools
{
    private static string SkillRoot => Path.Combine(AppContext.BaseDirectory, "EngineeringSkills");

    [McpServerTool, Description("Before drafting engineering geometry, list available CAD/NBR skills, then read cad-preparar-projeto and relevant discipline skills using read_engineering_skill. Guidance is not normative text or proof of compliance. Does not require the CAD app.")]
    public string ListEngineeringSkills()
    {
        var files = Directory.Exists(SkillRoot)
            ? Directory.GetFiles(SkillRoot, "SKILL.md", SearchOption.AllDirectories)
            : [];
        return JsonSerializer.Serialize(new
        {
            entrySkill = "cad-preparar-projeto",
            notice = "Read applicable skills before drawing. Verify normative editions and requirements with authorized sources. Loading is guidance, not a server-enforced drawing gate.",
            skills = files.OrderBy(path => path, StringComparer.Ordinal).Select(path => new
            {
                name = Path.GetFileName(Path.GetDirectoryName(path)),
                description = File.ReadLines(path).FirstOrDefault(line => line.StartsWith("description: ", StringComparison.Ordinal))?[13..]
            })
        });
    }

    [McpServerTool, Description("Read one bundled engineering SKILL.md before applying CAD operations. Start with cad-preparar-projeto; read nbr-consulta-e-evidencias and relevant NBR discipline skills. Supply the exact name from list_engineering_skills, never a path. No drawing changes.")]
    public string ReadEngineeringSkill(string name)
    {
        // Accept identifiers only; never allow a client-supplied file path.
        if (string.IsNullOrEmpty(name) || name.Length > 64 ||
            name.Any(c => c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))
            throw new ArgumentException("Use a skill name from list_engineering_skills.", nameof(name));
        var path = Path.Combine(SkillRoot, name, "SKILL.md");
        if (!File.Exists(path)) throw new ArgumentException("Unknown engineering skill. Call list_engineering_skills.", nameof(name));
        return File.ReadAllText(path);
    }
}
