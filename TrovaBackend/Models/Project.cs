namespace TrovaBackend.Models;

// Status values a posted project moves through. Bids/Guarantee/SubmittedWork/
// Reviews are deliberately NOT modelled as columns here — those will be
// separate tables with a ProjectId FK once those features land, so this
// table doesn't need reshaping when they do.
public static class ProjectStatus
{
    public const string OpenForBids = "open_for_bids";
    public const string Awarded = "awarded";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    // Added for My Projects / Project History / Project Detail. Internal
    // values are lower_snake_case; the read DTOs upper-case them at the
    // edge (e.g. "contractor_backed_off" -> "CONTRACTOR_BACKED_OFF") so no
    // separate mapping table is needed.
    public const string ContractorBackedOff = "contractor_backed_off";
    public const string GuaranteeRejectedByYou = "guarantee_rejected_by_you";
    public const string PendingReview = "pending_review";
    public const string Disputed = "disputed";
    public const string Failed = "failed";
}

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Public-facing identifier, format TRV-PRJ-XXXXX — this is what the UI
    // and other endpoints reference, never the raw Id.
    public string ProjectCode { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    // NOTE: column name kept as "ContractValueJod" to match the read-side
    // contract this was built against, but it holds the amount in whatever
    // Currency is set to below — NOT always JOD when multi-currency is used.
    public decimal ContractValueJod { get; set; }
    public string Currency { get; set; } = "JOD";

    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;

    // Full phrase as sent by the client (e.g. "Performance Guarantee"),
    // stored as-is — no enum mapping per the confirmed decision.
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int MinimumRequiredScore { get; set; }

    // "A" | "B" | "C" — label (e.g. "Class B") is derived at read time,
    // same pattern as CompanyDetails.ClassificationCode/ClassificationLabel.
    public string MinimumClassification { get; set; } = string.Empty;

    public DateTime BidSubmissionDeadline { get; set; }

    public string Status { get; set; } = ProjectStatus.OpenForBids;

    // Set once the owner picks a winning bid (status -> Awarded). Points at
    // the Bid the rest of the award/guarantee flow revolves around — who to
    // display as "Awarded to", whether they've confirmed yet, etc.
    public Guid? AwardedBidId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Set by POST /api/bids/{bidId}/mark-work-done (contractor declares
    // work complete). GET /projects/{id}/submitted-work reads off this —
    // that endpoint isn't built in this pass, but this is its trigger.
    public DateTime? SubmittedDate { get; set; }
}