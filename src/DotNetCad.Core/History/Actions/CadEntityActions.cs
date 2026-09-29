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

        if (originalEntity is CadLine line)
        {
            var (start, end) = CadConstructionMath.OffsetSegment(line.StartPoint, line.EndPoint, distance, toLeft: true);
            _newEntity = new CadLine(start, end, line.Layer, line.ColorHex);
        }
        else if (originalEntity is CadCircle circle)
        {
            _newEntity = new CadCircle(circle.Center, Math.Max(0.05, circle.Radius + distance), circle.Layer, circle.ColorHex);
        }
        else if (originalEntity is CadRectangle rect)
        {
            var vertices = rect.GetVertices();
            var offsetVertices = CadConstructionMath.OffsetPolygon(vertices, distance);
            _newEntity = new CadPolyline(offsetVertices, isClosed: true, layer: rect.Layer, colorHex: rect.ColorHex);
        }
        else if (originalEntity is CadPolyline poly)
        {
            var offsetVertices = CadConstructionMath.OffsetPolygon(poly.Vertices, distance);
            _newEntity = new CadPolyline(offsetVertices, isClosed: poly.IsClosed, layer: poly.Layer, colorHex: poly.ColorHex);
        }
        else
        {
            _newEntity = originalEntity.Clone();
            _newEntity.Translate(distance, distance);
        }
    }

    public string Description => $"Offset {_newEntity.EntityType}";

    public void Execute() => _document.AddEntity(_newEntity);
    public void Undo() => _document.RemoveEntity(_newEntity);
}
