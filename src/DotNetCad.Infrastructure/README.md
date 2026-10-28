# DotNetCad.Infrastructure

File interchange, format serialization, and external export mechanisms for **DotNetCadEngine**.

Like `DotNetCad.Core`, this project targets `.NET 10` with **zero UI dependencies**, making all importers and exporters fully functional in headless environments, CLI utilities, and server pipelines.

---

## Capabilities & Formats

### 1. Native Format (`.cad`)
* Custom JSON-based hierarchical serializer (`CadDrawingSerializer`).
* Persists full document state:
  - Document metadata and title.
  - Layer definitions (name, color, visibility, locked state).
  - Entities with exact geometry, GUID IDs, and styles.
  - Active document drafting preferences (snap toggles, hit radii, grid settings).

### 2. ASCII DXF Import & Export
* Focused, robust implementation for AutoCAD ASCII DXF interchange:
  - Supported entities: `LINE`, `CIRCLE`, `ARC`, `LWPOLYLINE`, and single-line `TEXT`.
  - Polylines and rectangles exported as `LWPOLYLINE`.
  - Dimensions exported as composite line/text blocks.
  - Color translation between hex ARGB and the standard AutoCAD Color Index (ACI).
  - Round-trip validation for basic 2D CAD workflows.

### 3. SVG Export
* Clean, scalable vector graphic export (`CadSvgExporter`):
  - Shapes grouped by visible layer (`<g id="layer-name">`).
  - Automatic conversion between CAD Cartesian coordinates (Y-up) and SVG canvas coordinates (Y-down).
  - Preserves stroke colors, line thickness, and geometry precision.

---

## Guidelines for Future Implementations

1. **Streaming & Memory Efficiency:** Large DXF or SVG files should stream tokens rather than buffering huge in-memory string concatenations.
2. **Entity Extensibility:** When adding new entity types to `DotNetCad.Core`, ensure corresponding mapping handlers are registered in `DxfReader`, `DxfWriter`, and `SvgWriter`.
3. **Lossless CAD Metadata:** When enhancing `.cad` serialization, maintain backwards compatibility through version tags.
