namespace TrovaBackend.DTOs.Bids;

// ── Responses ────────────────────────────────────────────────────────────
// Shared by Confirm and Back Off — both just report the resulting bid and
// project state back to the caller, nothing else to distinguish.
public class BidActionResponse
{
    public string BidId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;         // "CONFIRMED" | "BACKED_OFF"
    public string ProjectStatus { get; set; } = string.Empty;  // "AWARDED" | "CONTRACTOR_BACKED_OFF"
}
