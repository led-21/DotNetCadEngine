using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.History.Actions;

public sealed class AddEntityAction : IUndoableAction
{
    private readonly CadDocument _document;
    private readonly ICadEntity _entity;

    public AddEntityAction(CadDocument document, ICadEntity entity)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public string Description => $"Add {_entity.EntityType}";

    public void Execute() => _document.AddEntity(_entity);
    public void Undo() => _document.RemoveEntity(_entity);
}

public sealed class DeleteEntityAction : IUndoableAction
{
    private readonly CadDocument _document;
    private readonly ICadEntity _entity;
    private int _index = -1;

    public DeleteEntityAction(CadDocument document, ICadEntity entity)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public string Description => $"Delete {_entity.EntityType}";

    public void Execute()
    {
        _index = _document.Entities.IndexOf(_entity);
        _document.RemoveEntity(_entity);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _document.Entities.Count)
        {
            _document.Entities.Insert(_index, _entity);
        }
        else
        {
            _document.AddEntity(_entity);
        }
    }
}

public sealed class MoveEntityAction : IUndoableAction
{
    private readonly ICadEntity _entity;
    private readonly double _dx;
    private readonly double _dy;

    public MoveEntityAction(ICadEntity entity, double dx, double dy)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        if (double.IsNaN(dx) || double.IsInfinity(dx)) throw new ArgumentException("Invalid dx displacement", nameof(dx));
        if (double.IsNaN(dy) || double.IsInfinity(dy)) throw new ArgumentException("Invalid dy displacement", nameof(dy));

        _dx = dx;
        _dy = dy;
    }

    public string Description => $"Move {_entity.EntityType} ({_dx:+0.00;-0.00;0.00}, {_dy:+0.00;-0.00;0.00})";

    public void Execute() => _entity.Translate(_dx, _dy);
    public void Undo() => _entity.Translate(-_dx, -_dy);
}

public sealed class RotateEntityAction : IUndoableAction
{
    private readonly ICadEntity _entity;
    private readonly Point2D _origin;
    private readonly double _angleDegrees;

    public RotateEntityAction(ICadEntity entity, Point2D origin, double angleDegrees)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _origin = origin;
        _angleDegrees = angleDegrees;
    }

    public string Description => $"Rotate {_entity.EntityType} {_angleDegrees:F1}°";

    public void Execute() => _entity.Rotate(_origin, _angleDegrees);
    public void Undo() => _entity.Rotate(_origin, -_angleDegrees);
}

public sealed class MirrorEntityAction : IUndoableAction
{
    private readonly ICadEntity _entity;
    private readonly Point2D _axisP1;
    private readonly Point2D _axisP2;

    public MirrorEntityAction(ICadEntity entity, Point2D axisP1, Point2D axisP2)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _axisP1 = axisP1;
        _axisP2 = axisP2;
    }

    public string Description => $"Mirror {_entity.EntityType}";

    public void Execute() => _entity.Mirror(_axisP1, _axisP2);
    public void Undo() => _entity.Mirror(_axisP1, _axisP2);
}

public sealed class OffsetEntityAction : IUndoableAction
{
    private readonly CadDocument _document;
    private readonly ICadEntity _newEntity;

    public OffsetEntityAction(CadDocument document, ICadEntity originalEntity, double distance)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentNullException.ThrowIfNull(originalEntity);

        if (!double.IsFinite(distance) || Math.Abs(distance) < 0.01) throw new ArgumentException("Offset requer distância finita de pelo menos 0,01.");

        if (originalEntity is CadLine line)
        {
            var (start, end) = CadConstructionMath.OffsetSegment(line.StartPoint, line.EndPoint, distance, toLeft: true);
            _newEntity = new CadLine(start, end, line.Layer, line.ColorHex);
        }
        else if (originalEntity is CadCircle circle)
        {
            var validation = GeometryValidator.ValidateCircle(circle.Center, circle.Radius + distance);
            if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage);
            _newEntity = new CadCircle(circle.Center, circle.Radius + distance, circle.Layer, circle.ColorHex);
        }
        else if (originalEntity is CadRectangle rect)
        {
            var vertices = rect.GetVertices();
            var offsetVertices = CadConstructionMath.OffsetPolygon(vertices, distance);
            _newEntity = new CadPolyline(offsetVertices, isClosed: true, layer: rect.Layer, colorHex: rect.ColorHex);
        }
        else if (originalEntity is CadPolyline poly)
        {
            if (!poly.IsClosed) throw new ArgumentException("Offset de polilinha aberta não é suportado; use segmentos individuais.");
            var offsetVertices = CadConstructionMath.OffsetPolygon(poly.Vertices, distance);
            _newEntity = new CadPolyline(offsetVertices, isClosed: poly.IsClosed, layer: poly.Layer, colorHex: poly.ColorHex);
        }
        else
        {
            throw new ArgumentException("Offset suporta linhas, círculos, retângulos e polilinhas fechadas convexas.");
        }
        _newEntity.LineThickness = originalEntity.LineThickness;
        var bounds = _newEntity.BoundingBox;
        if (bounds.IsEmpty || !double.IsFinite(bounds.MinX) || !double.IsFinite(bounds.MinY) ||
            !double.IsFinite(bounds.MaxX) || !double.IsFinite(bounds.MaxY)) throw new ArgumentException("Offset produz geometria inválida.");
    }

    public string Description => $"Offset {_newEntity.EntityType}";

    public void Execute() => _document.AddEntity(_newEntity);
    public void Undo() => _document.RemoveEntity(_newEntity);
}

public sealed class ReplaceEntityAction : IUndoableAction
{
    private readonly CadDocument _document;
    private readonly ICadEntity _oldEntity;
    private readonly ICadEntity _newEntity;
    private readonly string _description;

    public ReplaceEntityAction(CadDocument document, ICadEntity oldEntity, ICadEntity newEntity, string? description = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _oldEntity = oldEntity ?? throw new ArgumentNullException(nameof(oldEntity));
        _newEntity = newEntity ?? throw new ArgumentNullException(nameof(newEntity));
        _description = description ?? $"Modificar {_oldEntity.EntityType}";
    }

    public string Description => _description;

    public void Execute()
    {
        var idx = _document.Entities.IndexOf(_oldEntity);
        if (idx >= 0)
        {
            _document.Entities[idx] = _newEntity;
        }
    }

    public void Undo()
    {
        var idx = _document.Entities.IndexOf(_newEntity);
        if (idx >= 0)
        {
            _document.Entities[idx] = _oldEntity;
        }
    }
}

public sealed class ChangeEntityLayerAction : IUndoableAction
{
    private readonly ICadEntity _entity;
    private readonly string _oldLayer;
    private readonly string _newLayer;

    public ChangeEntityLayerAction(ICadEntity entity, string newLayer)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _oldLayer = entity.Layer;
        _newLayer = newLayer ?? throw new ArgumentNullException(nameof(newLayer));
    }

    public string Description => $"Alterar camada de {_entity.EntityType} para {_newLayer}";

    public void Execute() => _entity.Layer = _newLayer;
    public void Undo() => _entity.Layer = _oldLayer;
}

public sealed class ChangeEntityColorAction : IUndoableAction
{
    private readonly ICadEntity _entity;
    private readonly string _oldColor;
    private readonly string _newColor;

    public ChangeEntityColorAction(ICadEntity entity, string newColor)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _oldColor = entity.ColorHex;
        _newColor = newColor ?? throw new ArgumentNullException(nameof(newColor));
    }

    public string Description => $"Alterar cor de {_entity.EntityType}";

    public void Execute() => _entity.ColorHex = _newColor;
    public void Undo() => _entity.ColorHex = _oldColor;
}
