using DotNetCad.Core.Editor;

namespace DotNetCad.Infrastructure.Dxf;

/// <summary>
/// Representa um aviso gerado durante o processamento de um arquivo DXF.
/// </summary>
public sealed record DxfImportWarning(string Code, string EntityType, int LineOrPairIndex, string Message);

/// <summary>
/// Resultado estruturado da importação de um arquivo DXF, incluindo o documento gerado,
/// a versão identificada do AutoCAD e relatórios de advertências/entidades ignoradas.
/// </summary>
public sealed class DxfImportResult
{
    public required CadDocument Document { get; init; }
    public string DetectedAcadVersion { get; init; } = "UNKNOWN";
    public int ImportedEntityCount { get; init; }
    public int SkippedEntityCount { get; init; }
    public List<DxfImportWarning> Warnings { get; init; } = [];

    public bool HasWarnings => Warnings.Count > 0;
}
