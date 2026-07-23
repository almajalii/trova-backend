namespace TrovaBackend.Models;

// Matches the Flutter notification feed's expected type enum exactly —
// see notification_model.dart:19-26. Uppercase snake case, stored as-is
// (same "no enum mapping table" pattern as Project.GuaranteeTypeRequired).
public static class NotificationType
{
    public const string ScoreIncreased = "SCORE_INCREASED";
    public const string GuaranteeIssued = "GUARANTEE_ISSUED";
    public const string GuaranteeExpiring = "GUARANTEE_EXPIRING";
    public const string ReviewReceived = "REVIEW_RECEIVED";
    public const string BidUnderReview = "BID_UNDER_REVIEW";
}

// No read/unread state, no pagination — the frontend model only handles a
// flat list today (confirmed scope). Message is pre-formatted server-side;
// the client only computes relative time ("2 hours ago") from Timestamp.
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; } // recipient

    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
