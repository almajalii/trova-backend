using System.ComponentModel.DataAnnotations;

namespace TrovaBackend.DTOs.Admin;

// ── Pending users (whitelist) ───────────────────────────────────────────

// GET /api/admin/users/pending — one entry per user with ApprovalStatus ==
// Pending. CompanyDetails is null if the user hasn't reached that step of
// signup yet (identity-verified but not yet submitted company details) —
// the admin can't meaningfully review them yet in that case, but they
// still show up so nothing silently disappears from the queue.
public class AdminPendingUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string RequestedDate { get; set; } = string.Empty; // "yyyy-MM-dd", = User.CreatedAt

    // Reuses the existing company-details record shape (includes banking
    // fields + computed classification) rather than duplicating it.
    public TrovaBackend.DTOs.CompanyDetailsRecordDto? CompanyDetails { get; set; }
}

public class AdminRejectUserRequest
{
    [Required(ErrorMessage = "A reason is required to reject an applicant.")]
    public string Reason { get; set; } = string.Empty;
}

// ── All users ────────────────────────────────────────────────────────────

// GET /api/admin/users — every Role == "user" account, whatever their
// approval status. Deliberately has no contractor/owner distinction: the
// app doesn't model one (a single account can post projects and bid on
// others), so there's nothing accurate to show here beyond "a user".
public class AdminUserSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; } // CompanyDetails.TradingName, falling back to LegalCompanyName
    public string ApprovalStatus { get; set; } = string.Empty;
    public string JoinedDate { get; set; } = string.Empty; // "yyyy-MM-dd", = User.CreatedAt
}

// ── Disputes ─────────────────────────────────────────────────────────────

// GET /api/admin/disputes — one entry per project that has ever been
// disputed (open or resolved).
public class AdminDisputeSummaryDto
{
    public string ProjectId { get; set; } = string.Empty; // ProjectCode
    public string ProjectTitle { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Open" | "Resolved"
    public string RaisedDate { get; set; } = string.Empty; // "yyyy-MM-dd"
}

// GET /api/admin/disputes/{projectId}
public class AdminDisputeDetailDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ContractValueJod { get; set; }
    public string TimelineText { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;

    public string ContractorName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty; // "Open" | "Resolved"
    public string RaisedDate { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string DisputeReason { get; set; } = string.Empty; // the owner's message

    // The contractor's original "done" submission — same data
    // ReviewWorkService.GetSubmittedWorkAsync shows the owner.
    public string SubmittedDate { get; set; } = string.Empty; // "yyyy-MM-dd"

    // Only set once resolved.
    public string? ResolutionMessage { get; set; }
    public string? ResolvedDate { get; set; } // "yyyy-MM-dd"
}

public class AdminResolveDisputeRequest
{
    [Required(ErrorMessage = "A resolution message is required.")]
    public string Message { get; set; } = string.Empty;
}
