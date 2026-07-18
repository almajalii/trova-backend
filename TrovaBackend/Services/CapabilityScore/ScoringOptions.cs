namespace TrovaBackend.Services.CapabilityScore;

// Bound from the "ScoringConfig" section of appsettings.json — same pattern
// as CompanyClassificationOptions. Change weights/benchmarks there, restart,
// no code changes. This is also the exact shape an Admin Scoring Config
// screen (if you build one later) would read and write, so nothing about
// the model needs to change if that gets added.
public class ScoringOptions
{
    public ScoringWeights Weights { get; set; } = new();
    public decimal BenchmarkAssetsValueJod { get; set; }

    // Policy constant, not bank data — the denominator in the Debt Capacity
    // formula. RemainingDebtCapacityJod (the numerator) is self-reported by
    // the user; JOFS has no field for either side of this ratio.
    public decimal RecommendedMaxDebtCapacityJod { get; set; }

    public ScoringTierThresholds TierThresholds { get; set; } = new();
}

public class ScoringWeights
{
    public decimal NumberOfCurrentDebts { get; set; }
    public decimal DebtCapacity { get; set; }
    public decimal CompanyAssetsValue { get; set; }
    public decimal DelinquentDebts { get; set; }
    public decimal PaymentHistory { get; set; }
    public decimal CurrentWorkload { get; set; }
    public decimal ProjectDeliveryHistory { get; set; }
    public decimal CashflowTrends { get; set; }
}

public class ScoringTierThresholds
{
    public int Strong { get; set; }   // score >= this → "Strong Capability"
    public int Moderate { get; set; } // score >= this → "Moderate Capability"
    // below Moderate → "Developing Capability"
}
