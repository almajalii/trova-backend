namespace TrovaBackend.Models;

public class CapabilityScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; } // one row per user, recalculated in place

    public int OverallScore { get; set; }
    public string TierLabel { get; set; } = string.Empty;

    // Internal-only stats (project history) — default to neutral values
    // until the Projects feature exists; wired up for real once it does.
    public int TotalProjects { get; set; }
    public int FailedProjects { get; set; }
    public double AvgRating { get; set; }

    // ── 8 scoring factors, flattened (score 0-100 + human-readable why) ──
    public int NumberOfCurrentDebtsScore { get; set; }
    public string NumberOfCurrentDebtsDescription { get; set; } = string.Empty;

    public int DebtCapacityScore { get; set; }
    public string DebtCapacityDescription { get; set; } = string.Empty;

    public int CompanyAssetsValueScore { get; set; }
    public string CompanyAssetsValueDescription { get; set; } = string.Empty;

    public int DelinquentDebtsScore { get; set; }
    public string DelinquentDebtsDescription { get; set; } = string.Empty;

    public int PaymentHistoryScore { get; set; }
    public string PaymentHistoryDescription { get; set; } = string.Empty;

    public int CurrentWorkloadScore { get; set; }
    public string CurrentWorkloadDescription { get; set; } = string.Empty;

    public int ProjectDeliveryHistoryScore { get; set; }
    public string ProjectDeliveryHistoryDescription { get; set; } = string.Empty;

    public int CashflowTrendsScore { get; set; }
    public string CashflowTrendsDescription { get; set; } = string.Empty;

    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;
}
