# DotNetCadEngine Tests

Comprehensive test suites for **DotNetCadEngine**, covering unit geometry math, tolerance policies, command processors, transaction integrity, and WPF viewport interactions.

The solution contains **239 automated tests** (100% pass rate) running on .NET 10.

---

## Test Suites

```
tests/
├── DotNetCad.Core.Tests/     # 177 Unit & Domain Tests (cross-platform net10.0)
│   ├── CadTrimExtendEngineTests.cs       # 20 tests: geometric trim/extend scenarios
│   ├── CadTrimExtendSessionTests.cs      # 6 tests: state machine & preview tests
│   ├── CadCommandProcessorTests.cs       # Console command execution & validation
│   ├── GeometryScaleAndPrecisionTests.cs # Multi-scale tolerance & precision tests
│   ├── Sprint4AiCadTests.cs              # AI staging, batch processing & rollback
│   └── ...                               # Snapping, selection, undo/redo & serialization
└── DotNetCad.Demo.Tests/     # 62 Integration Tests (Windows net10.0-windows)
    ├── TrimExtendWpfIntegrationTests.cs  # 4 tests: full viewport lifecycle & shortcuts
    ├── EditCompletionTests.cs            # Viewport mouse input & modification completion
    ├── LayoutMatrixTests.cs              # Viewport transformations & zoom math
    └── ...                               # Layers UI policy & property displays
```

---

## Running Tests

Run all tests across the entire solution:
```bash
dotnet test DotNetCadEngine.slnx
```

Run only Core domain tests (cross-platform on Windows and Linux):
```bash
dotnet test tests/DotNetCad.Core.Tests/DotNetCad.Core.Tests.csproj
```

Run WPF UI integration tests (requires Windows):
```bash
dotnet test tests/DotNetCad.Demo.Tests/DotNetCad.Demo.Tests.csproj
```

---

## Testing Patterns & Guidelines

### 1. Headless WPF STA Testing
WPF controls require single-threaded apartment (`STA`) threads and a presentation source. Tests in `DotNetCad.Demo.Tests` use the `Sta(...)` helper with `HwndSource`:

```csharp
[Fact]
public void Tool_InteractsWithViewport()
{
    Sta(() =>
    {
        using var host = new HwndSource(new HwndSourceParameters("Test Host") { Width = 800, Height = 600 });
        var doc = new CadDocument();
        var vp = new CadViewport { Document = doc, Width = 800, Height = 600 };
        host.RootVisual = vp;

        // Perform layout pass
        vp.Measure(new Size(800, 600));
        vp.Arrange(new Rect(0, 0, 800, 600));
        vp.UpdateLayout();

        // Simulate interaction using deterministic driver methods
        vp.HandleTrimExtendClick(new Point2D(5, 5));
    });
}
```

### 2. Transactional Undo/Redo Symmetry
Every test that asserts a document modification should verify both rollback and roll-forward:
```csharp
history.Undo();
Assert.Equal(initialGeometry, entity.Geometry);

history.Redo();
Assert.Equal(modifiedGeometry, entity.Geometry);
```

### 3. Layer Lock Verification
Whenever a new modification feature is added, include a test asserting that attempting to modify an entity on a locked layer (`layer.IsLocked = true`) is safely rejected without modifying the document or adding actions to history.
