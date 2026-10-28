using System.Globalization;

using DotNetCad.Core.Editor;

using DotNetCad.Core.Entities;

using DotNetCad.Core.Geometry;

using DotNetCad.Core.Measurements;

using DotNetCad.Demo.ViewModels;

using DotNetCad.Demo.ViewModels.Properties;

using DotNetCad.Infrastructure.Native;

using Xunit;



namespace DotNetCad.Demo.Tests;



public class PropertiesAndUnitsTests

{

    [Fact]

    public void UnitConverter_ConvertsLengthsCorrectly()

    {

        // 1.5 meters to mm and cm

        var mm = CadUnitConverter.FromMeters(1.5, "mm");

        var cm = CadUnitConverter.FromMeters(1.5, "cm");

        var m = CadUnitConverter.FromMeters(1.5, "m");



        Assert.Equal(1500.0, mm, precision: 4);

        Assert.Equal(150.0, cm, precision: 4);

        Assert.Equal(1.5, m, precision: 4);



        // Convert back to meters

        Assert.Equal(1.5, CadUnitConverter.ToMeters(1500, "mm"), precision: 4);

        Assert.Equal(1.5, CadUnitConverter.ToMeters(150, "cm"), precision: 4);

        Assert.Equal(1.5, CadUnitConverter.ToMeters(1.5, "m"), precision: 4);

    }



    [Fact]

    public void UnitConverter_FormatsWithUnitSuffix()

    {

        var formattedM = CadUnitConverter.FormatLength(2.543, "m", 2, true, CultureInfo.InvariantCulture);

        var formattedMm = CadUnitConverter.FormatLength(2.543, "mm", 0, true, CultureInfo.InvariantCulture);



        Assert.Equal("2.54 m", formattedM);

        Assert.Equal("2543 mm", formattedMm);

    }



    [Fact]

    public void UnitConverter_ParsesLengthsWithVariousFormats()

    {

        // Metric with suffixes and commas

        Assert.True(CadUnitConverter.TryParseLength("1500mm", "m", out var meters1, out _));

        Assert.Equal(1.5, meters1, precision: 4);



        Assert.True(CadUnitConverter.TryParseLength("25,4cm", "m", out var meters2, out _));

        Assert.Equal(0.254, meters2, precision: 4);



        Assert.True(CadUnitConverter.TryParseLength("12,50", "m", out var meters3, out _));

        Assert.Equal(12.50, meters3, precision: 4);



        // Rejects invalid text

        Assert.False(CadUnitConverter.TryParseLength("abc", "m", out _, out var error));

        Assert.NotEmpty(error);

    }



    [Fact]

    public void PropertyInspector_NoSelection_ShowsDocumentSettings()

    {

        var vm = new MainViewModel();

        var inspector = vm.Properties;

        inspector.Refresh();



        Assert.Equal(PropertyPanelMode.DocumentSettings, inspector.Mode);

        Assert.True(inspector.IsDocumentMode);

        Assert.Equal(vm.Document.Title, inspector.DocumentTitle);

        Assert.Equal("m", inspector.DisplayUnit);

    }



    [Fact]

    public void PropertyInspector_SingleLine_EditsLengthPreservingStartPoint()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0), "Geometry", "#38BDF8") { IsSelected = true };

        doc.AddEntity(line);



        var inspector = vm.Properties;

        inspector.Refresh();



        Assert.Equal(PropertyPanelMode.SingleEntity, inspector.Mode);

        Assert.True(inspector.IsLine);



        // Edit length from 10 to 25

        inspector.LineLength = "25";



        var updatedLine = Assert.IsType<CadLine>(doc.Entities[0]);

        Assert.Equal(0.0, updatedLine.StartPoint.X, precision: 4);

        Assert.Equal(0.0, updatedLine.StartPoint.Y, precision: 4);

        Assert.Equal(25.0, updatedLine.EndPoint.X, precision: 4);

        Assert.Equal(0.0, updatedLine.EndPoint.Y, precision: 4);

        Assert.Equal(25.0, updatedLine.Length, precision: 4);



        // Undo restores original length

        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoManager.Undo();

        var undoneLine = Assert.IsType<CadLine>(doc.Entities[0]);

        Assert.Equal(10.0, undoneLine.Length, precision: 4);

    }



    [Fact]

    public void PropertyInspector_Circle_ValidatesRadius()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        var circle = new CadCircle(new Point2D(5, 5), 3.0, "Geometry", "#38BDF8") { IsSelected = true };

        doc.AddEntity(circle);



        var inspector = vm.Properties;

        inspector.Refresh();



        // Valid edit

        inspector.CircleRadius = "7,5";

        var updated = Assert.IsType<CadCircle>(doc.Entities[0]);

        Assert.Equal(7.5, updated.Radius, precision: 4);



        // Invalid edit: negative or zero must be rejected

        inspector.CircleRadius = "-2";

        Assert.NotEmpty(inspector.ValidationError);

        // Radius remains 7.5

        Assert.Equal(7.5, Assert.IsType<CadCircle>(doc.Entities[0]).Radius, precision: 4);



        // Invalid edit: non-numeric text

        inspector.CircleRadius = "invalid";

        Assert.NotEmpty(inspector.ValidationError);

        Assert.Equal(7.5, Assert.IsType<CadCircle>(doc.Entities[0]).Radius, precision: 4);

    }



    [Fact]

    public void PropertyInspector_MultipleEntities_BatchLayerChange_SingleUndo()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        var line1 = new CadLine(new Point2D(0, 0), new Point2D(5, 5), "Geometry") { IsSelected = true };

        var line2 = new CadLine(new Point2D(5, 5), new Point2D(10, 10), "Hidden") { IsSelected = true };

        var circle = new CadCircle(new Point2D(0, 0), 2.0, "Geometry") { IsSelected = true };



        doc.AddEntity(line1);

        doc.AddEntity(line2);

        doc.AddEntity(circle);



        var inspector = vm.Properties;

        inspector.Refresh();



        Assert.Equal(PropertyPanelMode.MultipleEntities, inspector.Mode);

        Assert.Equal(3, inspector.SelectedCount);

        Assert.Equal("(Múltiplos valores)", inspector.CommonLayer);



        var initialUndoCount = vm.UndoManager.UndoCount;



        // Apply batch layer change to "Annotations"

        inspector.ApplyBatchLayer("Annotations");



        Assert.All(doc.Entities, e => Assert.Equal("Annotations", e.Layer));



        // Exact single undo operation generated

        Assert.Equal(initialUndoCount + 1, vm.UndoManager.UndoCount);



        // Undo restores original heterogeneous layers

        vm.UndoManager.Undo();

        Assert.Equal("Geometry", doc.Entities[0].Layer);

        Assert.Equal("Hidden", doc.Entities[1].Layer);

        Assert.Equal("Geometry", doc.Entities[2].Layer);

    }



    [Fact]

    public void Serialization_PreservesDisplayUnitAndPrecision()

    {

        var doc = new CadDocument();

        doc.Preferences.DisplayUnit = "mm";

        doc.Preferences.DecimalPrecision = 4;

        doc.AddEntity(new CadLine(new Point2D(1, 2), new Point2D(3, 4)));



        var json = CadDrawingSerializer.Serialize(doc);

        var restored = CadDrawingSerializer.Deserialize(json);



        Assert.Equal("mm", restored.Preferences.DisplayUnit);

        Assert.Equal(4, restored.Preferences.DecimalPrecision);

        Assert.Single(restored.Entities);

    }



    [Fact]

    public void CoordinateParser_ParsesPtBrCoordinatesAndDirectDistance()

    {

        // 1. Semicolon separator with comma decimals

        Assert.True(CadCoordinateParser.TryParse("12,5 ; -4,2", null, out var p1, out _));

        Assert.Equal(12.5, p1.X, precision: 4);

        Assert.Equal(-4.2, p1.Y, precision: 4);



        // 2. Relative coordinate with comma decimals

        var origin = new Point2D(10, 20);

        Assert.True(CadCoordinateParser.TryParse("@5,5 ; -10,0", origin, out var p2, out _));

        Assert.Equal(15.5, p2.X, precision: 4);

        Assert.Equal(10.0, p2.Y, precision: 4);



        // 3. Polar coordinate with angle

        Assert.True(CadCoordinateParser.TryParse("@10<0", origin, out var p3, out _));

        Assert.Equal(20.0, p3.X, precision: 4);

        Assert.Equal(20.0, p3.Y, precision: 4);



        // 4. Units in coordinate input

        Assert.True(CadCoordinateParser.TryParse("1500mm ; 50cm", null, out var p4, out _));

        Assert.Equal(1.5, p4.X, precision: 4);

        Assert.Equal(0.5, p4.Y, precision: 4);

    }



    [Fact]

    public void PropertyInspector_ColorPalette_ContainsStandardNamedColors()

    {

        var vm = new MainViewModel();

        var inspector = vm.Properties;



        Assert.NotEmpty(inspector.ColorPalette);

        Assert.Contains(inspector.ColorPalette, c => c.Hex == "#38BDF8" && c.Name == "Ciano");

        Assert.Contains(inspector.ColorPalette, c => c.Hex == "#EF4444" && c.Name == "Vermelho");

        Assert.Contains(inspector.ColorPalette, c => c.Hex == "#22C55E" && c.Name == "Verde");

        Assert.Contains(inspector.ColorPalette, c => c.Hex == "#FFFFFF" && c.Name == "Branco");

    }



    [Fact]

    public void PropertyInspector_SingleEntity_ChangesColor_WithUndo()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0), "Geometry", "#38BDF8") { IsSelected = true };

        doc.AddEntity(line);



        var inspector = vm.Properties;

        inspector.Refresh();



        Assert.Equal("#38BDF8", inspector.SingleEntityColor);



        // Change color via command or property

        inspector.SelectColorCommand.Execute("#EF4444");



        Assert.Equal("#EF4444", doc.Entities[0].ColorHex);

        Assert.Equal("#EF4444", inspector.SingleEntityColor);



        // Undo restores original color

        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoManager.Undo();

        Assert.Equal("#38BDF8", doc.Entities[0].ColorHex);

    }



    [Fact]

    public void PropertyInspector_MultipleEntities_BatchColorChange_SingleUndo()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        var line = new CadLine(new Point2D(0, 0), new Point2D(5, 5), "Geometry", "#38BDF8") { IsSelected = true };

        var circle = new CadCircle(new Point2D(2, 2), 3, "Geometry", "#22C55E") { IsSelected = true };

        doc.AddEntity(line);

        doc.AddEntity(circle);



        var inspector = vm.Properties;

        inspector.Refresh();



        Assert.Equal(PropertyPanelMode.MultipleEntities, inspector.Mode);

        Assert.Equal("(Múltiplos valores)", inspector.CommonColor);



        var undoCountBefore = vm.UndoManager.UndoCount;



        // Batch change to yellow

        inspector.SelectColorCommand.Execute("#EAB308");



        Assert.Equal("#EAB308", doc.Entities[0].ColorHex);

        Assert.Equal("#EAB308", doc.Entities[1].ColorHex);

        Assert.Equal(undoCountBefore + 1, vm.UndoManager.UndoCount);



        // Undo restores both distinct original colors

        vm.UndoManager.Undo();

        Assert.Equal("#38BDF8", doc.Entities[0].ColorHex);

        Assert.Equal("#22C55E", doc.Entities[1].ColorHex);

    }



    [Fact]

    public void PropertyInspector_LockedLayer_PreventsColorChange()

    {

        var vm = new MainViewModel();

        var doc = vm.Document;

        doc.Clear();



        doc.Layers.Add(new CadLayer { Name = "Construcao", ColorHex = "#94A3B8", IsLocked = true });

        var line = new CadLine(new Point2D(0, 0), new Point2D(5, 5), "Construcao", "#94A3B8") { IsSelected = true };

        doc.AddEntity(line);



        var inspector = vm.Properties;

        inspector.Refresh();



        // Attempting to change color on locked layer must be blocked with validation error

        inspector.SingleEntityColor = "#EF4444";



        Assert.NotEmpty(inspector.ValidationError);

        Assert.Equal("#94A3B8", doc.Entities[0].ColorHex);

    }

}

