namespace TrovaBackend.DTOs.Common;

/// <summary>
/// Sibling object added alongside any field that names a specific
/// contractor (e.g. "Awarded to Al-Fahad Contracting"), giving the
/// frontend a stable BidId it can hand to
/// GET /api/bids/{bidId}/company-profile to make that mention tappable.
///
/// Only ever attached when the surrounding text names one specific
/// contractor — omit/null it when the text is generic ("5 contractors
/// have submitted bids"), so the UI renders plain, non-tappable text.
/// </summary>
public class AwardedBidderDto
{
    public string BidId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    // "" if the contractor hasn't submitted Company Details yet.
    public string Classification { get; set; } = string.Empty;

    // Every caller here is resolving an *awarded* bid — eligibility was
    // already enforced at Award time — so this is always true in
    // practice. Kept as a real field (not hardcoded on the client) so a
    // future caller that isn't post-award can still set it accurately.
    public bool Eligible { get; set; } = true;
}
