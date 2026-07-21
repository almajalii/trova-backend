namespace TrovaBackend.Models;

// Lifecycle a Bid moves through. Submit-bid / select-winner / confirm
// endpoints aren't built yet (separate pass) — this table exists now so
// My Projects / Project History / Project Detail have something real to
// read bid counts and award state from instead of stored, staleable columns.
public static class BidStatus
{
    // Contractor has submitted a bid; owner hasn't picked a winner yet.
    public const string Submitted = "submitted";

    // Owner picked this bid as the winner; waiting on the contractor to
    // confirm. Project.Status flips to Awarded at this point.
    public const string PendingConfirmation = "pending_confirmation";

    // Contractor confirmed; now waiting on the bank to issue the guarantee.
    public const string Confirmed = "confirmed";

    // Contractor didn't confirm / declined. Project.Status flips to
    // ContractorBackedOff at this point.
    public const string BackedOff = "backed_off";

    // Another bid on the same project was awarded instead. Terminal —
    // this bid is no longer live.
    public const string NotSelected = "not_selected";
}

public class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public Guid ContractorId { get; set; }

    public decimal BidAmountJod { get; set; }

    public string Status { get; set; } = BidStatus.Submitted;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
