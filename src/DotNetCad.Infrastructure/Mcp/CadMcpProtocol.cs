using System;
using System.Collections.Generic;

namespace DotNetCad.Infrastructure.Mcp;

public static class CadMcpProtocol
{
    public static string PipeName => $"DotNetCadEngine.Mcp.v1.{Environment.UserName}";
}

/// <summary>
/// IPC request payload sent over Named Pipes between DotNetCad.Mcp and DotNetCad.Demo.
/// </summary>
public sealed record CadMcpRequest(
    string Method,
    string? Command = null,
    IReadOnlyList<string>? Commands = null,
    string? Summary = null,
    string? AgentIdentifier = null,
    Guid? ProposalId = null,
    string? Layer = null,
    string? LayerB = null,
    IReadOnlyList<string>? EntityTypes = null,
    string? TextContains = null,
    double? MinLength = null,
    double? MaxLength = null,
    double? WindowMinX = null,
    double? WindowMinY = null,
    double? WindowMaxX = null,
    double? WindowMaxY = null,
    bool AllowCrossing = true,
    double MinClearanceMeters = 0.0);

/// <summary>
/// Structured IPC response returned by DotNetCad.Demo to DotNetCad.Mcp.
/// Preserves backward compatibility with legacy <c>(Success, Message, Content)</c> consumers
/// while adding typed diagnostics, transaction IDs, and affected entity IDs for AI agents.
/// </summary>
public sealed record CadMcpResponse(
    bool Success,
    string Message,
    string? Content = null,
    string Code = "OK",
    string? SuggestedFix = null,
    Guid? TransactionId = null,
    Guid? ProposalId = null,
    IReadOnlyList<Guid>? CreatedEntityIds = null,
    IReadOnlyList<Guid>? ModifiedEntityIds = null,
    IReadOnlyList<Guid>? DeletedEntityIds = null);
