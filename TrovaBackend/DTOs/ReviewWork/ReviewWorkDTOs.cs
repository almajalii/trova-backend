using System.ComponentModel.DataAnnotations;
using TrovaBackend.DTOs.Common;

namespace TrovaBackend.DTOs.ReviewWork;

// POST /api/projects/{projectId}/flag-issue body — the owner's explanation
// for disputing the contractor's "done" claim. Shown to the admin
// resolving the dispute.
public class FlagIssueRequest
{
    [Required(ErrorMessage = "A reason is required to flag an issue.")]
    public string Reason { get; set; } = string.Empty;
}

// ── Submitted Work — GET /api/projects/{projectId}/submitted-work ──────────
// Owner-facing; only meaningful while the project's status is
// PENDING_REVIEW (i.e. MarkWorkDoneAsync has run). Matches
// submitted_work_model.dart's SubmittedWork.fromJson exactly.
public class SubmittedWorkDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;

    // Always set here — this endpoint always has a specific contractor.
    public AwardedBidderDto? AwardedBidder { get; set; }

    public string SubmittedDate { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string? GuaranteeRowText { get; set; }
}
