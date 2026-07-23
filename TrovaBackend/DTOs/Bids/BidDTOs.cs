namespace TrovaBackend.DTOs.Bids;

// ── My Bids (active) — GET /api/bids/mine ───────────────────────────────
// Also the shared response shape for Confirm / Back Off / Cancel /
// Mark Work Done — each returns the caller's full updated active list
// rather than just the one bid that changed.
public class MyBidItemDto
{
    public string BidId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty; // project owner's company
    public decimal BidAmountJod { get; set; }

    // PENDING | SELECTED | CONFIRMED | GUARANTEE_PENDING_REVIEW |
    // GUARANTEE_REJECTED | IN_PROGRESS | WORK_SUBMITTED
    //
    // CONFIRMED, GUARANTEE_PENDING_REVIEW, and GUARANTEE_REJECTED are all
    // "bid confirmed, guarantee not yet approved" — they only differ on
    // the linked GuaranteeApplication's state (none / pending / rejected;
    // see BidStatusMapper.ToExternal). Don't infer this split from Note
    // text; Note is free-form display copy and can change independently.
    // Once approved the bid itself moves to IN_PROGRESS — there's no
    // separate "GUARANTEE_APPROVED" bid status. WORK_SUBMITTED is the
    // same underlying Bid.Status (InProgress) as IN_PROGRESS, split out
    // once WorkSubmittedAt is set — "Mark Work as Done" should disappear
    // and something like "Awaiting Owner Review" should render instead.
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    // Present once Status == IN_PROGRESS and the bank has approved a
    // guarantee application for this bid — days remaining until that
    // application's ValidityExpiry, floored at 0. Null otherwise
    // (including IN_PROGRESS with no approved application, which
    // shouldn't happen given how a bid reaches InProgress today, but
    // isn't asserted against).
    public int? GuaranteeExpiresInDays { get; set; }

    // "yyyy-MM-dd", set once the contractor calls POST /bids/{id}/work-done.
    // Only present when Status == WORK_SUBMITTED; the Status field alone
    // is enough to drive UI state, this is here in case the date itself
    // is useful to show.
    public string? WorkSubmittedAt { get; set; }
}

// ── My Bids History (closed) — GET /api/bids/history ────────────────────
public class BidHistoryItemDto
{
    public string BidId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal BidAmountJod { get; set; }

    // COMPLETED | REJECTED | BACKED_OFF
    public string Status { get; set; } = string.Empty;

    // Required for REJECTED/BACKED_OFF, null for COMPLETED.
    public string? Note { get; set; }

    // Populated once the owner has left a review for this completed
    // project (via LeaveReviewService.SubmitReviewAsync). Null for the
    // "completed but not yet reviewed" empty state — that's a real state,
    // not a gap, so it's left to the frontend to render accordingly.
    // Flat, matching BidDetailDto's ReviewRating/ReviewText — the Flutter
    // client reads both off the top level, not nested.
    public int? ReviewRating { get; set; }
    public string? ReviewText { get; set; }
}

// ── Bid Detail — GET /api/bids/{bidId} ───────────────────────────────────
// New endpoint — didn't exist before this pass. Built against the
// frontend's contract, with one deliberate deviation: that contract
// status-gates Milestones/GuaranteeTypeRequired/PaymentTerms/Description
// (e.g. omitted for GUARANTEE_PENDING_REVIEW and IN_PROGRESS). This DTO
// populates them whenever the underlying Project has them, regardless of
// Status — cheaper to always send and let the frontend choose what to
// render than to couple this endpoint's shape to that screen's current
// layout. Worth confirming with frontend before relying on the gating.
public class BidDetailDto
{
    public string Id { get; set; } = string.Empty; // Bid.Id — string, per contract
    public string ProjectTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty; // project owner's company
    public string Status { get; set; } = string.Empty; // same vocabulary as MyBidItemDto.Status, plus REJECTED/COMPLETED/BACKED_OFF
    public string Sector { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string TimelineRange { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public List<BidStatusStepDto> StatusSteps { get; set; } = new();

    public string? Milestones { get; set; }
    public string? GuaranteeTypeRequired { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Description { get; set; }

    // Same computation as MyBidItemDto.GuaranteeExpiresInDays — only
    // non-null when Status == IN_PROGRESS with an Approved application.
    public int? GuaranteeExpiresInDays { get; set; }

    // Same as MyBidItemDto.WorkSubmittedAt.
    public string? WorkSubmittedAt { get; set; }

    // Populated for REJECTED (owner picked someone else) and
    // GUARANTEE_REJECTED (bank rejected the application) — reuses
    // Bid.Note / the same rejection copy those statuses already carry
    // elsewhere. Null for every other status.
    public string? BannerNote { get; set; }

    // Populated once the owner has left a review for this completed bid —
    // same empty-state reasoning as BidHistoryItemDto.Review.
    public int? ReviewRating { get; set; }
    public string? ReviewText { get; set; }
}

public class BidStatusStepDto
{
    public string Label { get; set; } = string.Empty;
    public string? Date { get; set; }

    // completed | current | rejected | pending (lowercase, per contract —
    // deliberately not reusing BidStatusMapper's SCREAMING_SNAKE here)
    public string State { get; set; } = "pending";
}