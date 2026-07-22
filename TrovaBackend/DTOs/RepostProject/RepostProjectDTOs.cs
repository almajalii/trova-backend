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
}

// ── Submit — POST /api/owner/projects/{projectId}/repost ───────────────────
// Body matches RepostProjectDraft.toJson() exactly — the frontend re-sends
// originalProjectId/reason/contractorName too even though the server
// already knows all three from the route + the original project; only
// Title/Sector/ContractValueJod/MinRequiredScore/MinContractorClassification/
// Description are actually editable, so only those are trusted here.
//
// MinContractorClassification is free text on this screen (e.g. "Class C
// or higher (Small+)"), unlike Post Project's bare A/B/C enum — the
// service extracts the letter out of whatever the owner typed.
public class RepostProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public int MinRequiredScore { get; set; }
    public string MinContractorClassification { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RepostProjectResponse
{
    public string NewProjectId { get; set; } = string.Empty;
}
