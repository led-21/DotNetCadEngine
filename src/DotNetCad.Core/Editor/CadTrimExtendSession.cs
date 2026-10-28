using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using DotNetCad.Core.Selection;

namespace DotNetCad.Core.Editor;

public sealed record CadTrimExtendOperationResult(
    bool Success,
    string Message,
    bool DrawingChanged,
    ICadEntity? ModifiedEntity = null);

/// <summary>
/// Interactive session for TRIM (Aparar) and EXTEND (Estender) tools.
/// Pure CAD Core state machine independent of WPF, managing boundary selection,
/// interactive hover previews, and atomic undoable executions.
/// </summary>
public sealed class CadTrimExtendSession
{
    private readonly List<ICadEntity> _boundaries = [];

    public CadToolType Tool { get; }
    public int Step { get; private set; } // 0 = Selecting boundaries; 1 = Trimming / Extending

    public IReadOnlyList<ICadEntity> Boundaries => _boundaries.AsReadOnly();
    public ICadEntity? HoverEntity { get; private set; }
    public TrimResult? HoverTrimResult { get; private set; }
    public ExtendResult? HoverExtendResult { get; private set; }
    public string Feedback { get; private set; } = string.Empty;

    public string Prompt => Step switch
    {
        0 when Tool == CadToolType.Trim => "Aparar: Selecione os limites de corte. Pressione Enter para confirmar. Esc cancela.",
        0 when Tool == CadToolType.Extend => "Estender: Selecione os limites de extensão. Pressione Enter para confirmar. Esc cancela.",
        1 when Tool == CadToolType.Trim => "Aparar: Clique no segmento que deseja aparar. Enter encerra. Esc cancela.",
        1 when Tool == CadToolType.Extend => "Estender: Clique próximo à extremidade desejada. Enter encerra. Esc cancela.",
        _ => string.Empty
    };

    public CadTrimExtendSession(CadToolType tool)
    {
        if (tool is not (CadToolType.Trim or CadToolType.Extend))
            throw new ArgumentException("Session must be created for Trim or Extend tool.", nameof(tool));

        Tool = tool;
        Step = 0;
    }

    /// <summary>
    /// Initializes boundaries with currently preselected entities if any.
    /// </summary>
    public void InitializePreselected(IEnumerable<ICadEntity> preselected)
    {
        foreach (var entity in preselected)
        {
            if (!_boundaries.Contains(entity))
            {
                _boundaries.Add(entity);
            }
        }
    }

    /// <summary>
    /// Toggles entity inclusion as a boundary during Step 0.
    /// </summary>
    public bool ToggleBoundary(ICadEntity entity)
    {
        if (Step != 0) return false;

        if (_boundaries.Contains(entity))
        {
            _boundaries.Remove(entity);
            Feedback = $"Limite removido: {entity.EntityType}.";
        }
        else
        {
            _boundaries.Add(entity);
            Feedback = $"Limite adicionado: {entity.EntityType}.";
        }

        return true;
    }

    /// <summary>
    /// Confirms boundary selection and moves to interactive Trim / Extend stage (Step 1).
    /// If no boundaries were explicitly selected, falls back to all visible entities in the document.
    /// </summary>
    public void ConfirmBoundaries(IEnumerable<ICadEntity>? allVisibleEntities = null)
    {
        if (_boundaries.Count == 0 && allVisibleEntities != null)
        {
            foreach (var entity in allVisibleEntities)
            {
                _boundaries.Add(entity);
            }
        }

        Step = 1;
        Feedback = _boundaries.Count > 0
            ? $"{_boundaries.Count} limite(s) confirmado(s). Agora selecione a geometria."
            : "Nenhum limite disponível no desenho.";
    }

    /// <summary>
    /// Updates real-time preview based on current cursor position over visible entities.
    /// </summary>
    public void Hover(Point2D worldPt, IEnumerable<ICadEntity> visibleEntities, double hitToleranceMeters, CadDocument document)
    {
        if (Step != 1)
        {
            ClearHover();
            return;
        }

        var hit = CadSelectionEngine.SelectByPoint(visibleEntities, worldPt, hitToleranceMeters);
        if (hit == null)
        {
            ClearHover();
            return;
        }

        HoverEntity = hit;

        // Check if entity is on a locked layer
        var layer = document.GetLayer(hit.Layer);
        if (layer?.IsLocked == true)
        {
            HoverTrimResult = null;
            HoverExtendResult = null;
            Feedback = $"A entidade está na camada bloqueada '{hit.Layer}'.";
            return;
        }

        if (Tool == CadToolType.Trim)
        {
            var trimRes = CadTrimExtendEngine.Trim(hit, _boundaries, worldPt);
            HoverTrimResult = trimRes;
            HoverExtendResult = null;
            Feedback = trimRes != null
                ? "Clique com o botão esquerdo para aparar o trecho destacado."
                : "Nenhuma interseção válida com os limites de corte.";
        }
        else
        {
            var extRes = CadTrimExtendEngine.Extend(hit, _boundaries, worldPt);
            HoverExtendResult = extRes;
            HoverTrimResult = null;
            Feedback = extRes != null
                ? "Clique com o botão esquerdo para estender até o limite."
                : "Nenhum limite intersecta a entidade na direção de extensão.";
        }
    }

    /// <summary>
    /// Executes the trim or extend operation on click.
    /// Returns true if successful and modifies the document with an undoable action.
    /// The session stays in Step 1 for repeated operations.
    /// </summary>
    public CadTrimExtendOperationResult ExecuteClick(
        Point2D clickPt,
        IEnumerable<ICadEntity> visibleEntities,
        double hitToleranceMeters,
        CadDocument document,
        UndoRedoManager history)
    {
        if (Step != 1)
        {
            return new(false, "Confirme os limites antes de executar a modificação.", false);
        }

        var hit = CadSelectionEngine.SelectByPoint(visibleEntities, clickPt, hitToleranceMeters);
        if (hit == null)
        {
            return new(false, "Nenhuma entidade selecionada.", false);
        }

        var layer = document.GetLayer(hit.Layer);
        if (layer?.IsLocked == true)
        {
            return new(false, $"A camada '{hit.Layer}' está bloqueada e não pode ser modificada.", false);
        }

        if (Tool == CadToolType.Trim)
        {
            var trimRes = CadTrimExtendEngine.Trim(hit, _boundaries, clickPt);
            if (trimRes == null)
            {
                return new(false, "A entidade não intersecta os limites de corte no trecho selecionado.", false);
            }

            var actions = new List<IUndoableAction> { new DeleteEntityAction(document, hit) };
            foreach (var rem in trimRes.RemainingEntities)
            {
                actions.Add(new AddEntityAction(document, rem));
            }

            history.ExecuteAction(new CompositeUndoableAction(actions, $"Aparar {hit.EntityType}"));

            ClearHover();
            return new(true, $"Entidade {hit.EntityType} aparada com sucesso.", true, trimRes.RemainingEntities.FirstOrDefault());
        }
        else
        {
            var extRes = CadTrimExtendEngine.Extend(hit, _boundaries, clickPt);
            if (extRes == null)
            {
                return new(false, "Nenhum limite intersecta a entidade na direção de extensão.", false);
            }

            history.ExecuteAction(new ReplaceEntityAction(document, hit, extRes.ExtendedEntity, $"Estender {hit.EntityType}"));

            ClearHover();
            return new(true, $"Entidade {hit.EntityType} estendida com sucesso.", true, extRes.ExtendedEntity);
        }
    }

    public void Cancel()
    {
        if (Step == 1)
        {
            Step = 0;
            ClearHover();
            Feedback = "Etapa cancelada. Selecione limites ou pressione Esc para sair.";
        }
        else
        {
            _boundaries.Clear();
            ClearHover();
            Feedback = string.Empty;
        }
    }

    public void ClearHover()
    {
        HoverEntity = null;
        HoverTrimResult = null;
        HoverExtendResult = null;
    }
}
