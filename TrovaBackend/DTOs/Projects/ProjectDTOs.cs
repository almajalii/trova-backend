using System.ComponentModel.DataAnnotations;

namespace TrovaBackend.DTOs.Projects;

// ── Requests ──────────────────────────────────────────────────────────────

// Matches the Post Project screen's draft object — POST /api/projects.
// No `status` field: every new project starts as OPEN_FOR_BIDS server-side.
public class PostProjectRequest
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sector is required")]
    public string Sector { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contract value is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Contract value cannot be negative")]
    public decimal ContractValue { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    [MaxLength(10)]
    public string Currency { get; set; } = "JOD";

    [Required(ErrorMessage = "Duration is required")]
    public string Duration { get; set; } = string.Empty;

    [Required(ErrorMessage = "Milestones are required")]
    public string Milestones { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bid submission deadline is required")]
    public DateTime BidSubmissionDeadline { get; set; }

    [Required(ErrorMessage = "Minimum required score is required")]
    [Range(0, 100, ErrorMessage = "Minimum required score must be between 0 and 100")]
    public int MinimumRequiredScore { get; set; }

    [Required(ErrorMessage = "Minimum classification is required")]
    [RegularExpression("^(A|B|C)$", ErrorMessage = "Minimum classification must be 'A', 'B', or 'C'")]
    public string MinimumClassification { get; set; } = string.Empty;

    [Required(ErrorMessage = "Guarantee type is required")]
    public string GuaranteeType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Payment terms are required")]
    public string PaymentTerms { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = string.Empty;
}

// ── Responses ─────────────────────────────────────────────────────────────

public class PostProjectResponse
{
    public string ProjectId { get; set; } = string.Empty; // TRV-PRJ-XXXXX
}

// ── My Projects (active list) — GET /api/projects/mine ──────────────────────
public class ProjectListItemDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    // OPEN_FOR_BIDS | AWARDED | CONTRACTOR_BACKED_OFF | GUARANTEE_REJECTED_BY_YOU
    // | IN_PROGRESS | PENDING_REVIEW
    public string Status { get; set; } = string.Empty;

    public decimal ContractValueJod { get; set; }

    public string? DetailText { get; set; }
    public string? GuaranteeStripLabel { get; set; }
    public string? GuaranteeStripSubtext { get; set; }
    public string? GuaranteeStripTone { get; set; } // "SUCCESS" | "WARNING" | null
    public string? Note { get; set; }
    public string? ActionLabel { get; set; }
}

// ── Project History — GET /api/projects/mine/history ────────────────────────
public class ProjectHistoryItemDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    // COMPLETED | DISPUTED | FAILED — mutually exclusive with the active list.
    public string Status { get; set; } = string.Empty;

    public decimal ContractValueJod { get; set; }

    public string? DetailText { get; set; }
    public string? GuaranteeStripLabel { get; set; }
    public string? GuaranteeStripSubtext { get; set; }
}

// ── Project Detail — GET /api/projects/{projectId} ──────────────────────────
public class TimelineStepDto
{
    public string Label { get; set; } = string.Empty;
    public string? Date { get; set; } // "yyyy-MM-dd" or null
    public string State { get; set; } = string.Empty; // DONE | ACTIVE | UPCOMING | FAILED
}

public class ProjectDetailDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Sector { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public string Location { get; set; } = string.Empty;
    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public string? GuaranteeRowText { get; set; }
    public string? BiddersRowText { get; set; }
    public List<TimelineStepDto> Timeline { get; set; } = new();
    public string? ActionLabel { get; set; }
    public bool ActionIsDanger { get; set; }
}

// ── Bidders / Compare — GET /api/projects/{projectId}/bids ─────────────────
public class BidSubFactorsDto
{
    public int CurrentDebts { get; set; }
    public int DebtCapacity { get; set; }
    public int AssetsValue { get; set; }
    public int DelinquentDebts { get; set; }
    public int PaymentHistory { get; set; }
    public int CurrentWorkload { get; set; }
    public int DeliveryHistory { get; set; }
    public int CashflowTrends { get; set; }
}

public class BidderDto
{
    public string BidId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int CapabilityScore { get; set; }
    public decimal BidAmountJod { get; set; }
    public string Classification { get; set; } = string.Empty; // "A" | "B" | "C" | ""
    public bool Eligible { get; set; }
    public BidSubFactorsDto SubFactors { get; set; } = new();
}

// ── Award — POST /api/projects/{projectId}/award ────────────────────────────
public class AwardProjectRequest
{
    [Required(ErrorMessage = "BidId is required")]
    public string BidId { get; set; } = string.Empty;
}

public class AwardProjectResponse
{
    public string ProjectId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AwardedCompanyName { get; set; } = string.Empty;
}

// ── Browse Projects (contractor side) — GET /api/projects/browse ───────────
public class BrowseProjectListItemDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PostedByCompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public int MinimumRequiredScore { get; set; }
    public string MinimumClassification { get; set; } = string.Empty; // "A" | "B" | "C"
    public string DaysLeftText { get; set; } = string.Empty; // "5 days left" / "Deadline passed"
}

// ── Browse Project Detail — GET /api/projects/browse/{projectId} ───────────
public class BrowseProjectDetailDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PostedByCompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public string GuaranteeTypeRequired { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public int MinimumRequiredScore { get; set; }
    public string MinimumClassification { get; set; } = string.Empty; // "A" | "B" | "C"
    public string MinimumClassificationText { get; set; } = string.Empty; // "Class B or higher"
    public string BidDeadlineText { get; set; } = string.Empty; // "July 19, 2026"
    public string Description { get; set; } = string.Empty;

    // True if this contractor already has a bid on this project — the
    // unique (ProjectId, ContractorId) index would reject a second one
    // anyway, this just lets the UI disable Submit Bid ahead of time.
    public bool AlreadyBid { get; set; }
}

// ── Submit Bid — POST /api/projects/browse/{projectId}/bid ──────────────────
public class SubmitBidRequest
{
    [Required(ErrorMessage = "Bid amount is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Bid amount cannot be negative")]
    public decimal BidAmountJod { get; set; }
}

public class SubmitBidResponse
{
    public string BidId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "SUBMITTED"
}