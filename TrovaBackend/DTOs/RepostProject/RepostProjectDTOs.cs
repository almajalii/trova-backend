namespace TrovaBackend.DTOs.RepostProject;

// Why an owner landed back on this form. Matches RepostReason.toJson() in
// repost_project_model.dart exactly.
public static class RepostReason
{
    public const string ContractorBackedOff = "contractor_backed_off";
    public const string GuaranteeRejectedByOwner = "guarantee_rejected_by_owner";
}

// ── Draft — GET /api/owner/projects/{projectId}/repost-draft ───────────────
// Only valid for a project sitting in ContractorBackedOff or
// GuaranteeRejectedByYou. Matches RepostProjectDraft.fromJson exactly.
//
// The first 9 fields below are hard casts on the frontend and throw on
// null — they must always be present and non-null in the response. The 7
// fields that follow are nullable-tolerant client-side.
public class RepostDraftDto
{
    public string OriginalProjectId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public int MinRequiredScore { get; set; }
    public string MinContractorClassification { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string TimelineText { get; set; } = string.Empty;

    // Single plain string, e.g. "Foundation – M3, Structure – M7,
    // Handover – M10" — NOT a list.
    public string Milestones { get; set; } = string.Empty;
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;

    // Serialized as ISO 8601 via System.Text.Json's default DateTime
    // handling — the frontend does DateTime.parse on this.
    public DateTime BidSubmissionDeadline { get; set; }
}

// ── Submit — POST /api/owner/projects/{projectId}/repost ───────────────────
// Body matches RepostProjectDraft.toJson() exactly — the frontend re-sends
// originalProjectId/reason/contractorName too even though the server
// already knows all three from the route + the original project, so only
// the fields below are trusted here.
//
// MinContractorClassification is now a bare "A" | "B" | "C", same as
// Post Project's enum — no free-text reconstruction on this screen anymore.
public class RepostProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public int MinRequiredScore { get; set; }
    public string MinContractorClassification { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public DateTime BidSubmissionDeadline { get; set; }
}

public class RepostProjectResponse
{
    public string NewProjectId { get; set; } = string.Empty;
}