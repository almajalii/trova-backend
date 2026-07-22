namespace TrovaBackend.DTOs.LeaveReview;

// The six rating categories, keyed the same snake_case way
// ReviewCategory.toJson() sends them in leave_review_model.dart. Kept as
// plain constants (not an enum) since the wire format is the string key
// itself, in a Dictionary<string, int> — matches how the frontend already
// serializes ratings.
public static class ReviewCategoryKeys
{
    public const string QualityOfWorkmanship = "quality_of_workmanship";
    public const string AdherenceToTimeline = "adherence_to_timeline";
    public const string AdherenceToBudgetScope = "adherence_to_budget_scope";
    public const string CommunicationResponsiveness = "communication_responsiveness";
    public const string SiteSafetyCompliance = "site_safety_compliance";
    public const string WouldYouRehire = "would_you_rehire";

    public static readonly string[] All =
    {
        QualityOfWorkmanship, AdherenceToTimeline, AdherenceToBudgetScope,
        CommunicationResponsiveness, SiteSafetyCompliance, WouldYouRehire
    };
}

// ── Context — GET /api/owner/projects/{projectId}/review-context ──────────
// Owner-facing. Only valid for a COMPLETED project the owner hasn't
// reviewed yet. Matches LeaveReviewDraft.fromJson — Ratings always comes
// back empty (0 = unrated in the frontend's model), since this is the
// blank-form starting point, not an existing review.
public class ReviewContextDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string CompletedDate { get; set; } = string.Empty; // "yyyy-MM-dd"
    public Dictionary<string, int> Ratings { get; set; } = new();
}

// ── Submit — POST /api/owner/projects/{projectId}/review ──────────────────
// Body matches LeaveReviewDraft.toJson(); projectId/contractorName/
// projectTitle/completedDate are also sent but not trusted — all of that
// is re-derived server-side from the route + the project record.
public class SubmitReviewRequest
{
    public Dictionary<string, int> Ratings { get; set; } = new();
    public string Comment { get; set; } = string.Empty;
}
