using System.Globalization;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;

namespace DotNetCad.Core.Editor;

/// <summary>Transient parameter collection. No document writes occur before Confirm.</summary>
public sealed class CadModificationSession(CadToolType tool)
{
    public CadToolType Tool { get; } = tool;
    public int Step { get; private set; }
    public Point2D? Origin { get; private set; }
    private Point2D _target;
    private double _value;
    private bool _keep;
    private ICadEntity[] _sources = [];
    public IReadOnlyList<ICadEntity> Preview { get; private set; } = [];
    public string Error { get; private set; } = "";
    public string Prompt => Step switch
    {
        0 => "Selecione entidades; Enter para continuar. Esc cancela.",
        1 when Tool == CadToolType.Offset => "Distância positiva em metros (ex.: 2,35).",
        1 => "Clique ou digite ponto base / pivô / primeiro ponto do eixo: x;y (m).",
        2 when Tool is CadToolType.Move or CadToolType.Copy => "Destino: clique ou x;y; vetor: @dx;dy; distância<ângulo (m/graus).",
        2 when Tool == CadToolType.Rotate => "Ângulo em graus ou clique direção a partir do pivô.",
        2 when Tool == CadToolType.Mirror => "Segundo ponto do eixo: clique ou x;y.",
        2 => "Clique no lado desejado (linhas: esquerda/direita; círculos: dentro/fora).",
        3 when Tool == CadToolType.Mirror => "Manter original? Digite sim ou não e Enter.",
        _ => "Preview: Enter confirma; Esc cancela."
    };
    public bool Select(IEnumerable<ICadEntity> entities)
    {
        _sources = entities.Where(e => e.IsSelected).ToArray();
        if (_sources.Length == 0) return Fail("Selecione ao menos uma entidade.");
        if (Tool == CadToolType.Offset && _sources.Any(e => e is not CadLine and not CadCircle))
            return Fail("Offset interativo suporta apenas linhas e círculos.");
        if (Tool == CadToolType.Mirror && _sources.Any(e => e is CadText))
            return Fail("Espelhamento de glifos de texto não é suportado.");
        Step = 1; Error = ""; return true;
    }
    public bool Input(string text)
    {
        text = text.Trim();
        if (Step == 3 && Tool == CadToolType.Mirror)
        {
            if (text is not ("sim" or "não" or "nao")) return Fail("Informe sim ou não.");
            _keep = text == "sim"; Step = 4; return Build();
        }
        if (Step == 1 && Tool == CadToolType.Offset || Step == 2 && Tool == CadToolType.Rotate)
        {
            if (!Number(text, out var n) || Tool == CadToolType.Offset && n < 0.01) return Fail("Valor finito inválido.");
            _value = n;
            if (Tool == CadToolType.Offset) { Step = 2; return true; }
            Step = 4; return Build();
        }
        if (Step == 2 && Tool is CadToolType.Move or CadToolType.Copy && text.Contains('<'))
        {
            var p = text.Split('<');
            if (p.Length != 2 || !Number(p[0], out var d) || !Number(p[1], out var a)) return Fail("Use distância<ângulo.");
            var r = a % 360 * Math.PI / 180;
            return Point(new(Origin!.Value.X + d * Math.Cos(r), Origin.Value.Y + d * Math.Sin(r)));
        }
        bool relative = text.StartsWith('@');
        var parts = text.TrimStart('@').Split(';');
        if (parts.Length != 2 || !Number(parts[0], out var x) || !Number(parts[1], out var y)) return Fail("Use x;y, com valores finitos.");
        if (relative && Origin is { } o) { x += o.X; y += o.Y; }
        return Point(new(x, y));
    }
    public bool Point(Point2D p)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) return Fail("Ponto inválido.");
        if (Step == 1 && Tool != CadToolType.Offset) { Origin = p; Step = 2; Error = ""; return true; }
        if (Step != 2) return false;
        _target = p;
        if (Tool == CadToolType.Rotate) _value = Math.Atan2(p.Y - Origin!.Value.Y, p.X - Origin.Value.X) * 180 / Math.PI;
        if (Tool == CadToolType.Mirror && Origin!.Value.DistanceTo(p) < 0.01) return Fail("Eixo precisa de dois pontos distintos.");
        Step = Tool == CadToolType.Mirror ? 3 : 4;
        return Build();
    }
    public void Hover(Point2D p)
    {
        if (Step != 2 || Tool == CadToolType.Rotate) return;
        var step = Step; Point(p); Step = step;
    }
    public bool Confirm(CadDocument document, UndoRedoManager history)
    {
        if (_sources.Any(e => document.GetLayer(e.Layer) is not { IsLocked: false })) return Fail("A seleção contém camada bloqueada.");
        if (Step != 4 || !Build()) return false;
        if (_sources.Any(e => !document.Entities.Contains(e))) return Fail("Seleção mudou; reinicie a operação.");
        var actions = new List<IUndoableAction>();
        for (int i = 0; i < _sources.Length; i++)
        {
            var e = _sources[i];
            if (Tool is CadToolType.Copy or CadToolType.Offset || Tool == CadToolType.Mirror && _keep)
                actions.Add(new AddEntityAction(document, Preview[i]));
            else if (Tool == CadToolType.Move) actions.Add(new MoveEntityAction(e, _target.X - Origin!.Value.X, _target.Y - Origin.Value.Y));
            else if (Tool == CadToolType.Rotate) actions.Add(new RotateEntityAction(e, Origin!.Value, _value % 360));
            else actions.Add(new MirrorEntityAction(e, Origin!.Value, _target));
        }
        history.ExecuteAction(new CompositeUndoableAction(actions, Tool.ToString()));
        return true;
    }
    private bool Build()
    {
        Preview = [];
        try
        {
            var result = new List<ICadEntity>();
            foreach (var e in _sources)
            {
                var c = e.Clone();
                if (Tool is CadToolType.Move or CadToolType.Copy)
                {
                    var dx = _target.X - Origin!.Value.X; var dy = _target.Y - Origin.Value.Y;
                    if (dx == 0 && dy == 0) return Fail("Deslocamento deve ser diferente de zero.");
                    c.Translate(dx, dy);
                }
                else if (Tool == CadToolType.Rotate) c.Rotate(Origin!.Value, _value % 360);
                else if (Tool == CadToolType.Mirror) c.Mirror(Origin!.Value, _target);
                else if (e is CadLine l)
                {
                    var cross = (l.EndPoint.X-l.StartPoint.X)*(_target.Y-l.StartPoint.Y)-(l.EndPoint.Y-l.StartPoint.Y)*(_target.X-l.StartPoint.X);
                    if (Math.Abs(cross) < 1e-9) return Fail("Escolha um lado fora da linha.");
                    var (a,b) = CadConstructionMath.OffsetSegment(l.StartPoint,l.EndPoint,_value,cross > 0);
                    c = new CadLine(a,b,e.Layer,e.ColorHex) { LineThickness = e.LineThickness };
                }
                else if (e is CadCircle circle)
                {
                    var radius = circle.Radius + (_target.DistanceTo(circle.Center) > circle.Radius ? _value : -_value);
                    if (radius < 0.05) return Fail("Offset resulta em raio inválido.");
                    c = new CadCircle(circle.Center,radius,e.Layer,e.ColorHex) { LineThickness = e.LineThickness };
                }
                var bnd = c.BoundingBox;
                if (bnd.IsEmpty || !double.IsFinite(bnd.MinX) || !double.IsFinite(bnd.MaxX) || !double.IsFinite(bnd.MinY) || !double.IsFinite(bnd.MaxY)) return Fail("Geometria resultante inválida.");
                result.Add(c);
            }
            Preview = result; Error = ""; return true;
        }
        catch (ArgumentException ex) { return Fail(ex.Message); }
    }
    private bool Fail(string error) { Preview = []; Error = error; return false; }
    private static bool Number(string text, out double n) => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n) && double.IsFinite(n);
}
