namespace TrovaBackend.DTOs.CapabilityScore;

// Matches GET /api/capability-score/me — same 8-factor shape already
// specified to the frontend (capability_score_model.dart), rule-based, no AI.
public class CapabilityScoreResponse
{
    public int OverallScore { get; set; }
    public string TierLabel { get; set; } = string.Empty;
    public ClassificationDto Classification { get; set; } = new();
    public TrackRecordStatsDto TrackRecordStats { get; set; } = new();
    public ScoreFactorsDto Factors { get; set; } = new();
}

public class ClassificationDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TrackRecordStatsDto
{
    public int TotalProjects { get; set; }
    public int FailedProjects { get; set; }

    // Awarded to this contractor and not yet completed, failed, or
    // cancelled (i.e. Project.Status is Awarded or InProgress). Same
    // "currently working on" signal as the CurrentWorkload score factor.
    public int CurrentProjects { get; set; }
    public double AvgRating { get; set; }
}

public class ScoreFactorDto
{
    public int Percentage { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ScoreFactorsDto
{
    public ScoreFactorDto NumberOfCurrentDebts { get; set; } = new();
    public ScoreFactorDto DebtCapacity { get; set; } = new();
    public ScoreFactorDto CompanyAssetsValue { get; set; } = new();
    public ScoreFactorDto DelinquentDebts { get; set; } = new();
    public ScoreFactorDto PaymentHistory { get; set; } = new();
    public ScoreFactorDto CurrentWorkload { get; set; } = new();
    public ScoreFactorDto ProjectDeliveryHistory { get; set; } = new();
    public ScoreFactorDto CashflowTrends { get; set; } = new();
}
