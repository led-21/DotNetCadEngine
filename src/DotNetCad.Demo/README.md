# DotNetCad.Demo

The desktop CAD presentation and interactive playground application for **DotNetCadEngine**.

Built on **WPF** and **.NET 10 (Windows)**, this project demonstrates real-time 2D CAD drafting, precision object snapping, graphical modification tools, and seamless integration with the AI/MCP infrastructure.

---

## Architecture & Components

```
DotNetCad.Demo/
├── Controls/
│   ├── CadViewport.cs       # Custom FrameworkElement rendering the CAD canvas
│   └── CadToolRail.xaml     # Vector-icon toolbars for Draw & Modify tools
├── ViewModels/
│   └── MainViewModel.cs     # Main MVVM viewmodel, command router, and console host
├── Mcp/
│   └── CadMcpPipeHost.cs    # Windows Named Pipe server bridging MCP tools to UI
└── Assets/                  # Application icons, themes, and visual resources
```

---

## Key Features

### 1. Viewport Rendering Pipeline (`CadViewport`)
* Uses low-overhead immediate-mode WPF drawing (`OnRender` with `DrawingContext`).
* Smooth panning and zooming with infinite world-space coordinates (`ViewportTransform`).
* Real-time visual feedback guides:
  - **Cutting Boundaries:** Amber dashed outline.
  - **Trim Preview:** Red dashed segment indicating the section to be discarded, with an intersection marker dot.
  - **Extend Preview:** Green dashed line showing the extension projection to the nearest boundary.
  - **On-Screen HUD:** Contextual step-by-step instructions and error warnings rendered in an unobtrusive overlay.

### 2. Interactive Tool Sessions
* Modification workflows delegate state to Core sessions (`CadTrimExtendSession`, `CadModificationSession`), keeping UI code focused on input translation and visual rendering:
  - `Enter` advances steps (e.g., confirming boundaries in Step 0) or completes the tool.
  - `Esc` cancels the current step or exits to the Selection tool.
  - Right-click acts as Enter for high-speed drafting efficiency.

### 3. Integrated CAD / AI Console
* Fast command-line input supporting standard aliases (`TR`, `EX`, `TRIM`, `EXTEND`, `APARAR`, `ESTENDER`).
* Shared undo history: graphical viewport operations and console commands operate on the same `UndoRedoManager`.

---

## Guidelines for Future Implementations

1. **Non-Mutating Previews:** Hover previews must never alter `CadDocument` or push actions to `UndoRedoManager`. Calculate preview geometry in memory or query session states.
2. **Layer Security:** Viewport operations must immediately verify layer lock status (`IsLocked`) before initiating interactive transformations.
3. **Headless Testability:** Expose deterministic simulation methods (like `HandleTrimExtendClick` and `HandleTrimExtendHover`) to enable robust STA integration testing without relying on OS mouse hooks.
