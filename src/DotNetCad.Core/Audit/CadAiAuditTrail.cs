using System;
using System.Collections.Generic;

namespace DotNetCad.Core.Audit;

/// <summary>
/// Categorizes the origin of an action recorded in the CAD document audit trail.
/// </summary>
public enum CadAuditActorType
{
    HumanUi,
    HumanConsole,
    McpAgent
}

/// <summary>
/// Immutable provenance record for an operation or atomic batch executed on a <see cref="Editor.CadDocument"/>.
/// Persisted inside native .cad files to ensure traceability of AI-assisted engineering drawings.
/// </summary>
public sealed record CadAiAuditEntry
{
    public Guid AuditId { get; init; } = Guid.NewGuid();
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public CadAuditActorType ActorType { get; init; } = CadAuditActorType.McpAgent;
    public string ActorIdentifier { get; init; } = "DotNetCad.Mcp";
    public string OperationName { get; init; } = string.Empty;
    public Guid? TransactionId { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<Guid> CreatedEntityIds { get; init; } = [];
    public IReadOnlyList<Guid> ModifiedEntityIds { get; init; } = [];
    public IReadOnlyList<Guid> DeletedEntityIds { get; init; } = [];
}
