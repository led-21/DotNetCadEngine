# DotNetCad.Core

The pure 2D CAD geometric modeling and calculation engine of **DotNetCadEngine**.

Targeted at `.NET 10`, `DotNetCad.Core` has **zero external NuGet dependencies** and **no UI framework references**, ensuring complete portability across Windows, Linux, and macOS.

---

## Architecture & Responsibilities

```
DotNetCad.Core/
├── Audit/           # Audit trail and event recording (CadAiAuditTrail)
├── Editor/          # Document model, command parsing, and modification sessions
├── Entities/        # Immutable/domain CAD entities (Line, Circle, Arc, Polyline, etc.)
├── Geometry/        # Computational geometry engines, tolerances, and math utilities
├── History/         # Command pattern Undo/Redo infrastructure and atomic actions
├── Queries/         # Semantic query engine for AI and analytical inspections
├── Snapping/        # Precision snapping solvers (Endpoints, Midpoints, Intersections)
└── Units/           # Unit systems and scale conversions
```

---

## 1. Computational Geometry

### `CadTrimExtendEngine`
* **Polymorphic Trimming:** Trims target entities against one or multiple cutting boundaries. Supports:
  - `CadLine`: Trims to segment intersections based on pick point.
  - `CadCircle`: Automatically converts into a `CadArc` representing the preserved angular sector.
  - `CadArc`: Recomputes start/end angles to preserve remaining sector(s).
  - `CadPolyline`: Splits vertices and edge segments while preserving overall continuity.
  - `CadRectangle`: Segments boundaries into lines or polylines.
* **Ray-Entity Extension:** Projects rays from entity endpoints (`START` or `END`) towards boundary entities, selecting the nearest valid forward intersection without shortening or zero-length extensions.
* **ID Preservation Policy:** The primary remaining fragment preserves the original entity's `Guid` ID. Any secondary fragments resulting from division receive newly generated IDs.

### `CadIntersectionEngine`
* Analytical intersection solvers for all 2D primitive pairs (Line-Line, Line-Circle, Line-Arc, Circle-Circle, Line-Polyline, etc.).
* Zero reliance on external geometry kernels.

### `GeometricTolerancePolicy`
* Centralized numerical tolerance management avoiding arbitrary floating-point heuristics:
  - `PointCoincidenceTolerance`: Default `1e-6` meters (or unit equivalent).
  - `CollinearTolerance`: Angular collinearity checks.
  - `MathEpsilon`: Discriminant and singular matrix thresholds (`1e-9`).

---

## 2. Editor & Session State Machines

* **`CadTrimExtendSession`**: Manages the multi-step interactive Trim/Extend lifecycle:
  - **Step 0:** Boundary selection (toggle individual entities or confirm all visible entities).
  - **Step 1:** Interactive trim/extend loop. Generates transient hover previews without document mutation.
  - Enforces layer lock verification (`IsLocked == true` blocks execution with clear feedback).
* **`CadModificationSession`**: Handles Move, Copy, Rotate, Mirror, Offset, and Break operations.
* **`CadAiStagingSession` & `CadBatchProcessor`**: Staging area for batch/AI instructions with atomic validation and complete rollback on failure.

---

## 3. History & Undo/Redo

All document mutations are encapsulated in `IUndoableAction`:
- `AddEntityAction` / `DeleteEntityAction`
- `ReplaceEntityAction`: Atomic 1-to-1 replacement preserving z-order.
- `CompositeUndoableAction`: Grouped atomic multi-entity edits (e.g., trimming a line into multiple pieces).

---

## 4. Guidelines for Future Implementations

1. **Keep Core Pure:** Never add dependencies on WPF (`System.Windows`), WinForms, or platform-specific APIs.
2. **Predictable IDs:** Always follow the ID preservation policy when modifying or subdividing geometry.
3. **Respect Tolerances:** Always query `GeometricTolerancePolicy` rather than hardcoding epsilon constants (`1e-4`, `0.001`).
4. **Layer Locking:** Any operation altering entities must verify `CadDocument.GetLayer(entity.Layer)?.IsLocked != true`.
