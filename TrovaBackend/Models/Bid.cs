namespace TrovaBackend.Models;

// Lifecycle a Bid moves through. Submit-bid / select-winner / confirm
// endpoints aren't built yet (separate pass) — this table exists now so
// My Projects / Project History / Project Detail have something real to
// read bid counts and award state from instead of stored, staleable columns.
//
// External API vocabulary (My Bids feature) maps onto these internal
// values as: Submitted->PENDING, PendingConfirmation->SELECTED,
// Confirmed->CONFIRMED (or GUARANTEE_PENDING_REVIEW once a
// GuaranteeApplication exists for the bid), InProgress->IN_PROGRESS,
// NotSelected->REJECTED, BackedOff->BACKED_OFF, Completed->COMPLETED. See
// BidStatusMapper.ToExternal in BidService.cs.
public static class BidStatus
{
    // Contractor has submitted a bid; owner hasn't picked a winner yet.
    public const string Submitted = "submitted";

    // Owner picked this bid as the winner; waiting on the contractor to
    // confirm. Project.Status flips to Awarded at this point.
    public const string PendingConfirmation = "pending_confirmation";

    // Contractor confirmed; now waiting on the bank to issue the guarantee.
    public const string Confirmed = "confirmed";

    // Guarantee approved, work underway. Nothing in this codebase
    // transitions a bid into this state yet — that's the guarantee
    // approval flow, speced/built separately.
    public const string InProgress = "in_progress";

    // Contractor didn't confirm / declined, or backed off later (from
    // Confirmed or InProgress). Project.Status flips to
    // ContractorBackedOff at this point.
    public const string BackedOff = "backed_off";

    // Another bid on the same project was awarded instead. Terminal —
    // this bid is no longer live.
    public const string NotSelected = "not_selected";

    // Owner confirmed the work as done. Nothing in this codebase
    // transitions a bid into this state yet — that's the owner-side
    // "confirm work done" flow, not part of this pass.
    public const string Completed = "completed";
}

public class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public Guid ContractorId { get; set; }

    public decimal BidAmountJod { get; set; }

    public string Status { get; set; } = BidStatus.Submitted;

    // Human-readable context for the My Bids / My Bids History cards —
    // e.g. "You backed off", "Bid cancelled", "Owner selected a different
    // bidder". Null while still active (Pending/Selected/Confirmed/
    // InProgress); those states derive their own display text at read
    // time instead of being stored.
    public string? Note { get; set; }

    // Set by MarkWorkDoneAsync alongside the Project.Status flip to
    // PendingReview. Bid.Status deliberately stays InProgress (Completed
    // is reserved for the owner's confirm-complete step), so without this
    // the frontend had no server-provided way to tell "in progress, not
    // yet submitted" apart from "in progress, awaiting owner review" —
    // see BidStatusMapper.ToExternal's WORK_SUBMITTED case.
    public DateTime? WorkSubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
