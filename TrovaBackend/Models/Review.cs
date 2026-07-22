namespace TrovaBackend.Models;

// The six rating categories on the "Leave a Review" screen (see
// ReviewCategory in leave_review_model.dart), stored as fixed columns
// rather than a child table — small, known, unchanging set, no need for
// the extra join. All six are rated 1-5, including WouldYouRehire (the
// frontend treats it as a star rating like the rest, not a yes/no).
public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // One review per completed project — enforced by a unique index on
    // ProjectId, not by RevieweeId, since the reviewer/reviewee pair
    // could otherwise repeat across different projects together.
    public Guid ProjectId { get; set; }
    public Guid ReviewerId { get; set; } // the project owner
    public Guid RevieweeId { get; set; } // the contractor being reviewed

    public int QualityOfWorkmanship { get; set; }
    public int AdherenceToTimeline { get; set; }
    public int AdherenceToBudgetScope { get; set; }
    public int CommunicationResponsiveness { get; set; }
    public int SiteSafetyCompliance { get; set; }
    public int WouldYouRehire { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
