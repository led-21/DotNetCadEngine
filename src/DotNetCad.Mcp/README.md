# DotNetCad.Mcp

The **Model Context Protocol (MCP)** server enabling AI assistants (such as Antigravity, Claude Desktop, Cursor, and custom LLM agents) to programmatically inspect, draft, modify, and export drawings in **DotNetCadEngine**.

---

## How It Works

```mermaid
flowchart LR
    AI[AI Agent / LLM Client] <-- stdio (JSON-RPC) --> McpServer[DotNetCad.Mcp]
    McpServer <-- Windows Named Pipe --> DesktopApp[DotNetCad.Demo (WPF)]
```

* **Live Canvas Synchronization:** Changes made by the MCP tools are applied immediately to the active drawing and rendered in the WPF desktop app.
* **Shared History:** AI edits are added to the same Undo/Redo stack as manual user edits.
* **Non-Destructive Inspection:** Tools like `describe_drawing` and `get_entity_properties` return structured geometric descriptions without modifying document state.

---

## Available MCP Tools

| Category | Tools |
| :--- | :--- |
| **Inspection & Queries** | `describe_drawing`, `get_entity_properties`, `get_layer_properties` |
| **Drafting** | `add_line`, `add_circle`, `add_rectangle`, `add_arc`, `add_polyline`, `add_text`, `add_dimension` |
| **Modifications** | `trim_line`, `extend_line`, `break_line`, `offset_entity`, `move_entity`, `copy_entity`, `rotate_entity`, `mirror_entity`, `delete_entity` |
| **Layers & Visibility** | `create_layer`, `delete_layer`, `rename_layer`, `set_layer`, `set_layer_color`, `hide_layer`, `show_layer`, `set_layer_locked` |
| **View & File Interchange** | `zoom_extents`, `new_drawing`, `open_drawing`, `save_drawing`, `export_dxf`, `export_svg` |
| **Command Execution** | `execute_cad_command` (runs any valid CAD console command string) |

---

## Client Configuration

To register the server in your MCP client configuration (e.g. `claude_desktop_config.json` or `antigravity_mcp.json`):

```json
{
  "mcpServers": {
    "dotnetcad": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\adria\\source\\repos\\DotNetCadEngine\\src\\DotNetCad.Mcp\\bin\\Debug\\net10.0\\DotNetCad.Mcp.dll"
      ]
    }
  }
}
```

*Note: The WPF desktop app (`DotNetCad.Demo`) must be running so the named pipe connection can be established.*

---

## Guidelines for Future Tool Additions

1. **Atomic & Reversible:** Every modifying tool must execute through `CadCommandProcessor` or an equivalent `IUndoableAction` so users can undo AI operations.
2. **Clear Parameter Types:** Provide explicit schemas and descriptions for double coordinates, radians/degrees, and entity ID parameters.
3. **Graceful Failures:** If an entity is locked or does not intersect, return an informative error message rather than crashing the stdio process.
