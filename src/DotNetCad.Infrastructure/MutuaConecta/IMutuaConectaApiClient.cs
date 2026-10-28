namespace DotNetCad.Infrastructure.MutuaConecta;

public sealed record MutuaConnectionStatus(
    bool IsHealthy,
    string? DatabaseStatus = null,
    string? Service = null,
    string? ErrorMessage = null
);

public sealed record MutuaUserSummaryDto(
    Guid UserId,
    Guid ProfileId,
    string Email,
    string FullName,
    string Title,
    string? PrimarySpecialtyCode,
    string? StateUf
);

public sealed record MutuaAuthResult(
    bool IsSuccess,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    MutuaUserSummaryDto? User = null,
    string? ErrorMessage = null
);

public sealed record MutuaProjectSummaryDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string City,
    string StateUf,
    string Status,
    Guid OwnerProfileId,
    string OwnerName,
    int DisciplineCount,
    int OpenRequirementCount,
    int MemberCount,
    DateTimeOffset UpdatedAtUtc
);

public sealed record MutuaProjectDisciplineDto(
    Guid Id,
    Guid SpecialtyId,
    string SpecialtyCode,
    string Name,
    string? CadLayerMapping,
    string Status
);

public sealed record MutuaProjectRequirementDto(
    Guid Id,
    Guid? ProjectDisciplineId,
    Guid RequiredSpecialtyId,
    string RequiredSpecialtyCode,
    string RequiredSpecialtyName,
    Guid RequiredCompetencyId,
    string RequiredCompetencyCode,
    string RequiredCompetencyName,
    string MinProficiencyLevel,
    string? RequiredStateUf,
    bool RequiresLocalPresence,
    string Title,
    string TechnicalNotes,
    decimal? EstimatedHonorariumBrl,
    string Status
);

public sealed record MutuaProjectMemberDto(
    Guid Id,
    Guid ProfessionalProfileId,
    string DisplayName,
    string ProfessionalTitle,
    string SpecialtyName,
    string StateUf,
    Guid? ProjectDisciplineId,
    string RoleInProject,
    string AssignedResponsibility,
    DateTimeOffset JoinedAtUtc
);

public sealed record MutuaDocumentSummaryDto(
    Guid Id,
    Guid? ProjectDisciplineId,
    Guid UploadedByProfileId,
    string UploadedByName,
    string Title,
    string FileName,
    string Format,
    int VersionNumber,
    string Sha256Hash,
    long FileSizeBytes,
    string? ContentPreviewSvg,
    string Visibility,
    DateTimeOffset UploadedAtUtc
);

public sealed record MutuaProjectDetailDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string City,
    string StateUf,
    bool AllowsRemoteCollaboration,
    string Status,
    decimal? EstimatedBudgetBrl,
    Guid OwnerUserId,
    Guid OwnerProfileId,
    string OwnerName,
    IReadOnlyList<MutuaProjectDisciplineDto> Disciplines,
    IReadOnlyList<MutuaProjectRequirementDto> Requirements,
    IReadOnlyList<MutuaProjectMemberDto> Members,
    IReadOnlyList<MutuaDocumentSummaryDto> Documents
);

public sealed record MutuaProfessionalMatchDto(
    Guid ProfileId,
    string DisplayName,
    string Title,
    string PrimarySpecialtyCode,
    string PrimarySpecialtyName,
    string City,
    string StateUf,
    int Score,
    IReadOnlyList<string> MatchReasons,
    bool AlreadyInvited,
    bool AlreadyInTeam
);

public sealed record MutuaCreateInvitationRequest(
    Guid ProjectRequirementId,
    Guid RecipientProfileId,
    string Message,
    decimal? ProposedHonorariumBrl
);

public sealed record MutuaInvitationDto(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    Guid ProjectRequirementId,
    string RequirementTitle,
    Guid SenderProfileId,
    string SenderName,
    Guid RecipientProfileId,
    string RecipientName,
    string Message,
    decimal? ProposedHonorariumBrl,
    string Status,
    DateTimeOffset CreatedAtUtc
);

public sealed record MutuaCadDocumentSyncRequest(
    Guid? ProjectDisciplineId,
    string Title,
    string FileName,
    string Format,
    string Sha256Hash,
    long FileSizeBytes,
    string? CadMetadataJson,
    string? ContentPreviewSvg,
    string Visibility = "ProjectTeam"
);

public sealed record MutuaDocumentSyncResult(
    bool IsSuccess,
    Guid? DocumentId = null,
    int? VersionNumber = null,
    string? Message = null,
    string? ErrorMessage = null
);

public interface IMutuaConectaApiClient
{
    string BaseUrl { get; }
    string? SessionToken { get; set; }

    void SetBaseUrl(string url);

    Task<MutuaConnectionStatus> CheckHealthAsync(CancellationToken ct = default);
    Task<MutuaAuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<IReadOnlyList<MutuaProjectSummaryDto>> GetMyProjectsAsync(CancellationToken ct = default);
    Task<MutuaProjectDetailDto?> GetProjectDetailsAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<MutuaProfessionalMatchDto>> GetRecommendedSpecialistsAsync(
        Guid projectId, Guid requirementId, CancellationToken ct = default);
    Task<MutuaInvitationDto?> SendCollaborationInvitationAsync(
        Guid projectId, MutuaCreateInvitationRequest request, CancellationToken ct = default);
    Task<MutuaDocumentSyncResult> SyncCadDocumentMetadataAsync(
        Guid projectId, MutuaCadDocumentSyncRequest request, CancellationToken ct = default);
}
