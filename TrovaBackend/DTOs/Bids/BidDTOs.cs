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

    // PENDING | SELECTED | CONFIRMED | IN_PROGRESS
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    // Only present when Status == IN_PROGRESS. Stubbed null for now —
    // guarantee expiry isn't tracked anywhere in the schema yet (the
    // guarantee approval flow this depends on is speced/built separately).
    public int? GuaranteeExpiresInDays { get; set; }
}

// ── My Bids History (closed) — GET /api/bids/history ────────────────────
public class BidHistoryItemDto
{
    public string BidId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal BidAmountJod { get; set; }

    // COMPLETED | REJECTED | WITHDRAWN
    public string Status { get; set; } = string.Empty;

    // Required for REJECTED/WITHDRAWN, null for COMPLETED.
    public string? Note { get; set; }

    // Only ever populated on COMPLETED bids, once the owner has left a
    // review. Always null for now — no Review entity exists in the schema
    // yet, and the empty-state ("completed but not yet reviewed") design
    // still needs product input per the handoff notes.
    public BidReviewDto? Review { get; set; }
}

public class BidReviewDto
{
    public int Stars { get; set; }
    public string Comment { get; set; } = string.Empty;
}
