using System;
using System.Collections.Generic;
using System.Linq;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Queries;

/// <summary>
/// Filter criteria for semantic entity queries executed by AI agents or the CAD console.
/// </summary>
public sealed record CadSemanticFilter
{
    public string? Layer { get; init; }
    public IReadOnlyList<string>? EntityTypes { get; init; }
    public BoundingBox2D? Window { get; init; }
    public bool AllowCrossing { get; init; } = true;
    public string? TextContains { get; init; }
    public double? MinLength { get; init; }
    public double? MaxLength { get; init; }
}

/// <summary>
/// Summary descriptor for an entity matched by <see cref="CadSemanticQueryEngine.QueryEntities"/>.
/// </summary>
public sealed record CadQueriedEntitySummary
{
    public Guid Id { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Layer { get; init; } = string.Empty;
    public string ColorHex { get; init; } = string.Empty;
    public double LengthMeters { get; init; }
    public double ClosedAreaSquareMeters { get; init; }
    public BoundingBox2D BoundingBox { get; init; }
    public string? TextContent { get; init; }
    public string CoordinatesSummary { get; init; } = string.Empty;
}

/// <summary>
/// Result of a semantic entity query.
/// </summary>
public sealed record CadEntityQueryResult
{
    public int TotalDocumentEntities { get; init; }
    public int MatchedCount { get; init; }
    public BoundingBox2D? MatchedBounds { get; init; }
    public double TotalMatchedLengthMeters { get; init; }
    public double TotalMatchedClosedAreaSquareMeters { get; init; }
    public IReadOnlyList<CadQueriedEntitySummary> Entities { get; init; } = [];
}

/// <summary>
/// Represents a detected geometric intersection or minimum clearance violation between two entities.
/// </summary>
public sealed record CadSpatialConflict
{
    public string ConflictType { get; init; } = "INTERSECTION";
    public Guid EntityAId { get; init; }
    public string EntityAType { get; init; } = string.Empty;
    public string LayerA { get; init; } = string.Empty;
    public Guid EntityBId { get; init; }
    public string EntityBType { get; init; } = string.Empty;
    public string LayerB { get; init; } = string.Empty;
    public Point2D Location { get; init; }
    public double DistanceMeters { get; init; }
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Per-layer quantitative and geometric summary.
/// </summary>
public sealed record CadLayerTopologySummary
{
    public string LayerName { get; init; } = string.Empty;
    public string ColorHex { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
    public bool IsLocked { get; init; }
    public int EntityCount { get; init; }
    public IReadOnlyDictionary<string, int> CountByType { get; init; } = new Dictionary<string, int>();
    public double TotalLinearLengthMeters { get; init; }
    public double TotalClosedAreaSquareMeters { get; init; }
    public BoundingBox2D? LayerBounds { get; init; }
}

/// <summary>
/// Represents a closed geometric region (room, silo, boundary, lot) and any text labels positioned inside it.
/// </summary>
public sealed record CadEnclosedRegionSummary
{
    public Guid BoundaryEntityId { get; init; }
    public string BoundaryEntityType { get; init; } = string.Empty;
    public string Layer { get; init; } = string.Empty;
    public double PerimeterMeters { get; init; }
    public double AreaSquareMeters { get; init; }
    public BoundingBox2D Bounds { get; init; }
    public IReadOnlyList<string> EnclosedLabels { get; init; } = [];
}

/// <summary>
/// High-level semantic and topological summary of a <see cref="CadDocument"/>.
/// </summary>
public sealed record CadTopologyAnalysisResult
{
    public int TotalEntities { get; init; }
    public int TotalLayers { get; init; }
    public double TotalLinearLengthMeters { get; init; }
    public double TotalClosedAreaSquareMeters { get; init; }
    public BoundingBox2D? DocumentBounds { get; init; }
    public IReadOnlyList<CadLayerTopologySummary> Layers { get; init; } = [];
    public IReadOnlyList<CadEnclosedRegionSummary> EnclosedRegions { get; init; } = [];
}

/// <summary>
/// Provides high-level spatial, semantic, and clash-detection queries over a <see cref="CadDocument"/>
/// so AI agents can inspect engineering drawings without downloading thousands of raw vertices.
/// </summary>
public static class CadSemanticQueryEngine
{
    /// <summary>
    /// Queries entities in <paramref name="document"/> matching <paramref name="filter"/>.
    /// </summary>
    public static CadEntityQueryResult QueryEntities(CadDocument document, CadSemanticFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        filter ??= new CadSemanticFilter();

        var typeSet = filter.EntityTypes is { Count: > 0 }
            ? filter.EntityTypes
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var matched = new List<CadQueriedEntitySummary>();
        BoundingBox2D? combinedBounds = null;
        double totalLength = 0;
        double totalArea = 0;

        foreach (var entity in document.Entities)
        {
            if (!string.IsNullOrWhiteSpace(filter.Layer) &&
                !string.Equals(entity.Layer, filter.Layer.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entityTypeStr = entity.EntityType.ToString();
            if (typeSet is not null && !typeSet.Contains(entityTypeStr.ToUpperInvariant()))
            {
                continue;
            }

            var bbox = entity.BoundingBox;
            if (filter.Window.HasValue)
            {
                var window = filter.Window.Value;
                var matchesWindow = filter.AllowCrossing
                    ? window.Intersects(bbox)
                    : window.Contains(bbox);
                if (!matchesWindow)
                {
                    continue;
                }
            }

            var textContent = ExtractEntityText(entity);
            if (!string.IsNullOrWhiteSpace(filter.TextContains))
            {
                if (string.IsNullOrEmpty(textContent) ||
                    !textContent.Contains(filter.TextContains.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var length = ComputeEntityLength(entity);
            if (filter.MinLength.HasValue && length < filter.MinLength.Value)
            {
                continue;
            }

            if (filter.MaxLength.HasValue && length > filter.MaxLength.Value)
            {
                continue;
            }

            var area = ComputeEntityClosedArea(entity);

            totalLength += length;
            totalArea += area;
            combinedBounds = combinedBounds.HasValue ? combinedBounds.Value.Expand(bbox) : bbox;

            matched.Add(new CadQueriedEntitySummary
            {
                Id = entity.Id,
                EntityType = entityTypeStr,
                Layer = entity.Layer,
                ColorHex = entity.ColorHex,
                LengthMeters = Math.Round(length, 4),
                ClosedAreaSquareMeters = Math.Round(area, 4),
                BoundingBox = bbox,
                TextContent = textContent,
                CoordinatesSummary = FormatCoordinatesSummary(entity)
            });
        }

        return new CadEntityQueryResult
        {
            TotalDocumentEntities = document.Entities.Count,
            MatchedCount = matched.Count,
            MatchedBounds = combinedBounds,
            TotalMatchedLengthMeters = Math.Round(totalLength, 4),
            TotalMatchedClosedAreaSquareMeters = Math.Round(totalArea, 4),
            Entities = matched
        };
    }

    /// <summary>
    /// Detects geometric intersections and minimum-clearance violations between entities
    /// (optionally filtered between <paramref name="layerA"/> and <paramref name="layerB"/>).
    /// </summary>
    public static IReadOnlyList<CadSpatialConflict> FindSpatialConflicts(
        CadDocument document,
        string? layerA = null,
        string? layerB = null,
        double minClearanceMeters = 0.0,
        GeometricTolerancePolicy? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var tol = tolerance ?? GeometricTolerancePolicy.Default;
        var clearance = Math.Max(0.0, minClearanceMeters);

        var entities = document.Entities.ToList();
        var conflicts = new List<CadSpatialConflict>();

        for (var i = 0; i < entities.Count; i++)
        {
            var e1 = entities[i];
            for (var j = i + 1; j < entities.Count; j++)
            {
                var e2 = entities[j];

                if (!MatchesLayerPair(e1.Layer, e2.Layer, layerA, layerB))
                {
                    continue;
                }

                var expand = Math.Max(clearance, tol.PointCoincidenceTolerance * 10);
                var b1 = ExpandBoundingBox(e1.BoundingBox, expand);
                var b2 = ExpandBoundingBox(e2.BoundingBox, expand);
                if (!b1.Intersects(b2))
                {
                    continue;
                }

                // 1. Exact geometric intersections
                var intersections = CadIntersectionEngine.IntersectEntityEntity(e1, e2, tol);
                if (intersections.Count > 0)
                {
                    var reported = false;
                    foreach (var pt in intersections)
                    {
                        var isSharedEndpointSameLayer =
                            string.Equals(e1.Layer, e2.Layer, StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrWhiteSpace(layerA) &&
                            string.IsNullOrWhiteSpace(layerB) &&
                            IsEntityEndpoint(e1, pt, tol) &&
                            IsEntityEndpoint(e2, pt, tol);

                        if (isSharedEndpointSameLayer)
                        {
                            continue;
                        }

                        conflicts.Add(new CadSpatialConflict
                        {
                            ConflictType = "INTERSECTION",
                            EntityAId = e1.Id,
                            EntityAType = e1.EntityType.ToString(),
                            LayerA = e1.Layer,
                            EntityBId = e2.Id,
                            EntityBType = e2.EntityType.ToString(),
                            LayerB = e2.Layer,
                            Location = pt,
                            DistanceMeters = 0.0,
                            Description = $"Interseção geométrica entre {e1.EntityType} (layer '{e1.Layer}') e {e2.EntityType} (layer '{e2.Layer}') em ({pt.X:F3}, {pt.Y:F3})."
                        });
                        reported = true;
                        break;
                    }

                    if (reported)
                    {
                        continue;
                    }
                }

                // 2. Minimum clearance violation check
                if (clearance > tol.PointCoincidenceTolerance)
                {
                    var (minDist, closestPt) = ComputeMinDistanceBetweenEntities(e1, e2);
                    if (minDist > tol.PointCoincidenceTolerance && minDist < clearance)
                    {
                        conflicts.Add(new CadSpatialConflict
                        {
                            ConflictType = "CLEARANCE_VIOLATION",
                            EntityAId = e1.Id,
                            EntityAType = e1.EntityType.ToString(),
                            LayerA = e1.Layer,
                            EntityBId = e2.Id,
                            EntityBType = e2.EntityType.ToString(),
                            LayerB = e2.Layer,
                            Location = closestPt,
                            DistanceMeters = Math.Round(minDist, 4),
                            Description = $"Violação de afastamento mínimo ({minDist:F3}m < {clearance:F3}m) entre {e1.EntityType} ('{e1.Layer}') e {e2.EntityType} ('{e2.Layer}') próximo a ({closestPt.X:F3}, {closestPt.Y:F3})."
                        });
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Computes per-layer quantitative summaries (counts, linear lengths, closed areas) and identifies
    /// enclosed spaces/rooms with their internal <see cref="CadText"/> labels.
    /// </summary>
    public static CadTopologyAnalysisResult AnalyzeTopology(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var tol = GeometricTolerancePolicy.Default;

        var layerSummaries = new List<CadLayerTopologySummary>();
        var allLayerNames = document.Layers.Select(l => l.Name)
            .Union(document.Entities.Select(e => e.Layer), StringComparer.OrdinalIgnoreCase)
            .ToList();

        double docTotalLength = 0;
        double docTotalArea = 0;
        BoundingBox2D? docBounds = null;

        foreach (var layerName in allLayerNames)
        {
            var layerObj = document.GetLayer(layerName);
            var layerEntities = document.Entities
                .Where(e => string.Equals(e.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var countByType = layerEntities
                .GroupBy(e => e.EntityType.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            double layerLength = 0;
            double layerArea = 0;
            BoundingBox2D? layerBounds = null;

            foreach (var entity in layerEntities)
            {
                var len = ComputeEntityLength(entity);
                var area = ComputeEntityClosedArea(entity);
                var bbox = entity.BoundingBox;

                layerLength += len;
                layerArea += area;
                layerBounds = layerBounds.HasValue ? layerBounds.Value.Expand(bbox) : bbox;
            }

            docTotalLength += layerLength;
            docTotalArea += layerArea;
            if (layerBounds.HasValue)
            {
                docBounds = docBounds.HasValue ? docBounds.Value.Expand(layerBounds.Value) : layerBounds.Value;
            }

            layerSummaries.Add(new CadLayerTopologySummary
            {
                LayerName = layerObj?.Name ?? layerName,
                ColorHex = layerObj?.ColorHex ?? "#FFFFFF",
                IsVisible = layerObj?.IsVisible ?? true,
                IsLocked = layerObj?.IsLocked ?? false,
                EntityCount = layerEntities.Count,
                CountByType = countByType,
                TotalLinearLengthMeters = Math.Round(layerLength, 4),
                TotalClosedAreaSquareMeters = Math.Round(layerArea, 4),
                LayerBounds = layerBounds
            });
        }

        var textEntities = document.Entities.OfType<CadText>().ToList();
        var enclosedRegions = new List<CadEnclosedRegionSummary>();

        foreach (var entity in document.Entities)
        {
            if (entity is CadRectangle rect)
            {
                var polygon = rect.GetVertices();
                var area = rect.Area;
                if (area <= tol.MinPolygonArea) continue;

                var labels = textEntities
                    .Where(t => !string.IsNullOrWhiteSpace(t.Text) && PolygonMath.IsPointInsidePolygon(t.Position, polygon))
                    .Select(t => t.Text.Trim())
                    .ToList();

                enclosedRegions.Add(new CadEnclosedRegionSummary
                {
                    BoundaryEntityId = rect.Id,
                    BoundaryEntityType = rect.EntityType.ToString(),
                    Layer = rect.Layer,
                    PerimeterMeters = Math.Round(rect.Perimeter, 4),
                    AreaSquareMeters = Math.Round(area, 4),
                    Bounds = rect.BoundingBox,
                    EnclosedLabels = labels
                });
            }
            else if (entity is CadPolyline { IsClosed: true, Vertices.Count: >= 3 } poly)
            {
                var area = poly.Area;
                if (area <= tol.MinPolygonArea) continue;

                var labels = textEntities
                    .Where(t => !string.IsNullOrWhiteSpace(t.Text) && PolygonMath.IsPointInsidePolygon(t.Position, poly.Vertices))
                    .Select(t => t.Text.Trim())
                    .ToList();

                enclosedRegions.Add(new CadEnclosedRegionSummary
                {
                    BoundaryEntityId = poly.Id,
                    BoundaryEntityType = poly.EntityType.ToString(),
                    Layer = poly.Layer,
                    PerimeterMeters = Math.Round(poly.Length, 4),
                    AreaSquareMeters = Math.Round(area, 4),
                    Bounds = poly.BoundingBox,
                    EnclosedLabels = labels
                });
            }
            else if (entity is CadCircle circle && circle.Area > tol.MinPolygonArea)
            {
                var labels = textEntities
                    .Where(t => !string.IsNullOrWhiteSpace(t.Text) && circle.Center.DistanceTo(t.Position) <= circle.Radius)
                    .Select(t => t.Text.Trim())
                    .ToList();

                enclosedRegions.Add(new CadEnclosedRegionSummary
                {
                    BoundaryEntityId = circle.Id,
                    BoundaryEntityType = circle.EntityType.ToString(),
                    Layer = circle.Layer,
                    PerimeterMeters = Math.Round(circle.Circumference, 4),
                    AreaSquareMeters = Math.Round(circle.Area, 4),
                    Bounds = circle.BoundingBox,
                    EnclosedLabels = labels
                });
            }
        }

        return new CadTopologyAnalysisResult
        {
            TotalEntities = document.Entities.Count,
            TotalLayers = layerSummaries.Count,
            TotalLinearLengthMeters = Math.Round(docTotalLength, 4),
            TotalClosedAreaSquareMeters = Math.Round(docTotalArea, 4),
            DocumentBounds = docBounds,
            Layers = layerSummaries,
            EnclosedRegions = enclosedRegions
        };
    }

    public static double ComputeEntityLength(ICadEntity entity) => entity switch
    {
        CadLine line => line.Length,
        CadCircle circle => circle.Circumference,
        CadArc arc => arc.ArcLength,
        CadRectangle rect => rect.Perimeter,
        CadPolyline poly => poly.Length,
        CadDimension dim => dim.CalculatedLengthMeters,
        _ => 0.0
    };

    public static double ComputeEntityClosedArea(ICadEntity entity) => entity switch
    {
        CadCircle circle => circle.Area,
        CadRectangle rect => rect.Area,
        CadPolyline { IsClosed: true, Vertices.Count: >= 3 } poly => poly.Area,
        _ => 0.0
    };

    private static string? ExtractEntityText(ICadEntity entity) => entity switch
    {
        CadText txt => txt.Text,
        CadDimension dim => dim.FormattedText,
        _ => null
    };

    private static string FormatCoordinatesSummary(ICadEntity entity) => entity switch
    {
        CadLine line => $"({line.StartPoint.X:F2},{line.StartPoint.Y:F2}) -> ({line.EndPoint.X:F2},{line.EndPoint.Y:F2})",
        CadCircle circle => $"Center=({circle.Center.X:F2},{circle.Center.Y:F2}), R={circle.Radius:F2}",
        CadArc arc => $"Center=({arc.Center.X:F2},{arc.Center.Y:F2}), R={arc.Radius:F2}, Ang={arc.StartAngleDegrees:F1}..{arc.EndAngleDegrees:F1}",
        CadRectangle rect => $"Origin=({rect.Origin.X:F2},{rect.Origin.Y:F2}), W={rect.Width:F2}, H={rect.Height:F2}",
        CadPolyline poly => $"{poly.Vertices.Count} pts, Closed={poly.IsClosed}",
        CadText txt => $"Pos=({txt.Position.X:F2},{txt.Position.Y:F2}), Text='{txt.Text}'",
        CadDimension dim => $"({dim.StartPoint.X:F2},{dim.StartPoint.Y:F2}) <-> ({dim.EndPoint.X:F2},{dim.EndPoint.Y:F2}) ({dim.FormattedText})",
        _ => string.Empty
    };

    private static bool MatchesLayerPair(string entityLayer1, string entityLayer2, string? filterLayerA, string? filterLayerB)
    {
        var hasA = !string.IsNullOrWhiteSpace(filterLayerA);
        var hasB = !string.IsNullOrWhiteSpace(filterLayerB);

        if (!hasA && !hasB)
        {
            return true;
        }

        if (hasA && !hasB)
        {
            var a = filterLayerA!.Trim();
            return string.Equals(entityLayer1, a, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entityLayer2, a, StringComparison.OrdinalIgnoreCase);
        }

        if (!hasA && hasB)
        {
            var b = filterLayerB!.Trim();
            return string.Equals(entityLayer1, b, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entityLayer2, b, StringComparison.OrdinalIgnoreCase);
        }

        var targetA = filterLayerA!.Trim();
        var targetB = filterLayerB!.Trim();

        return (string.Equals(entityLayer1, targetA, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entityLayer2, targetB, StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(entityLayer1, targetB, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entityLayer2, targetA, StringComparison.OrdinalIgnoreCase));
    }

    private static BoundingBox2D ExpandBoundingBox(BoundingBox2D box, double margin) =>
        new(box.MinX - margin, box.MinY - margin, box.MaxX + margin, box.MaxY + margin);

    private static bool IsEntityEndpoint(ICadEntity entity, Point2D pt, GeometricTolerancePolicy tol) => entity switch
    {
        CadLine line => line.StartPoint.DistanceTo(pt) <= tol.PointCoincidenceTolerance ||
                        line.EndPoint.DistanceTo(pt) <= tol.PointCoincidenceTolerance,
        CadArc arc => arc.StartPoint.DistanceTo(pt) <= tol.PointCoincidenceTolerance ||
                      arc.EndPoint.DistanceTo(pt) <= tol.PointCoincidenceTolerance,
        CadPolyline { IsClosed: false, Vertices.Count: >= 2 } poly =>
            poly.Vertices[0].DistanceTo(pt) <= tol.PointCoincidenceTolerance ||
            poly.Vertices[^1].DistanceTo(pt) <= tol.PointCoincidenceTolerance,
        _ => false
    };

    private static (double MinDistance, Point2D ClosestPoint) ComputeMinDistanceBetweenEntities(ICadEntity e1, ICadEntity e2)
    {
        var samples1 = SampleEntityPoints(e1);
        var samples2 = SampleEntityPoints(e2);

        var bestDist = double.MaxValue;
        var bestMid = Point2D.Zero;

        foreach (var p1 in samples1)
        {
            var proj2 = ProjectPointOntoEntity(e2, p1);
            var d = p1.DistanceTo(proj2);
            if (d < bestDist)
            {
                bestDist = d;
                bestMid = new Point2D((p1.X + proj2.X) * 0.5, (p1.Y + proj2.Y) * 0.5);
            }
        }

        foreach (var p2 in samples2)
        {
            var proj1 = ProjectPointOntoEntity(e1, p2);
            var d = p2.DistanceTo(proj1);
            if (d < bestDist)
            {
                bestDist = d;
                bestMid = new Point2D((p2.X + proj1.X) * 0.5, (p2.Y + proj1.Y) * 0.5);
            }
        }

        return (bestDist, bestMid);
    }

    private static Point2D ProjectPointOntoEntity(ICadEntity entity, Point2D pt)
    {
        var segments = CadIntersectionEngine.GetEntitySegments(entity);
        if (segments.Count > 0)
        {
            var best = segments[0].Start;
            var bestDist = double.MaxValue;
            foreach (var (s, e) in segments)
            {
                var proj = ProjectPointOntoSegment(pt, s, e);
                var d = pt.DistanceTo(proj);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = proj;
                }
            }
            return best;
        }

        if (entity is CadCircle circle)
        {
            var d = circle.Center.DistanceTo(pt);
            if (d < 1e-9) return new Point2D(circle.Center.X + circle.Radius, circle.Center.Y);
            return new Point2D(
                circle.Center.X + (pt.X - circle.Center.X) / d * circle.Radius,
                circle.Center.Y + (pt.Y - circle.Center.Y) / d * circle.Radius);
        }

        if (entity is CadArc arc)
        {
            var d = arc.Center.DistanceTo(pt);
            if (d > 1e-9)
            {
                var candidate = new Point2D(
                    arc.Center.X + (pt.X - arc.Center.X) / d * arc.Radius,
                    arc.Center.Y + (pt.Y - arc.Center.Y) / d * arc.Radius);
                if (CadIntersectionEngine.IsAngleOnArc(candidate, arc))
                {
                    return candidate;
                }
            }
            return pt.DistanceTo(arc.StartPoint) <= pt.DistanceTo(arc.EndPoint) ? arc.StartPoint : arc.EndPoint;
        }

        if (entity is CadText txt)
        {
            return txt.Position;
        }

        return entity.BoundingBox.Center;
    }

    private static Point2D ProjectPointOntoSegment(Point2D p, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq < 1e-12) return a;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0.0, 1.0);
        return new Point2D(a.X + t * dx, a.Y + t * dy);
    }

    private static List<Point2D> SampleEntityPoints(ICadEntity entity)
    {
        var pts = new List<Point2D>();
        var segments = CadIntersectionEngine.GetEntitySegments(entity);
        if (segments.Count > 0)
        {
            foreach (var (s, e) in segments)
            {
                for (var i = 0; i <= 4; i++)
                {
                    var t = i / 4.0;
                    pts.Add(new Point2D(s.X + (e.X - s.X) * t, s.Y + (e.Y - s.Y) * t));
                }
            }
            return pts;
        }

        switch (entity)
        {
            case CadCircle circle:
                for (var i = 0; i < 16; i++)
                {
                    var rad = i * Math.PI * 2.0 / 16.0;
                    pts.Add(new Point2D(
                        circle.Center.X + circle.Radius * Math.Cos(rad),
                        circle.Center.Y + circle.Radius * Math.Sin(rad)));
                }
                break;
            case CadArc arc:
                pts.Add(arc.StartPoint);
                pts.Add(arc.Midpoint);
                pts.Add(arc.EndPoint);
                break;
            case CadText txt:
                pts.Add(txt.Position);
                break;
            case CadDimension dim:
                pts.Add(dim.StartPoint);
                pts.Add(dim.EndPoint);
                pts.Add(dim.OffsetPoint);
                break;
        }

        return pts;
    }
}
