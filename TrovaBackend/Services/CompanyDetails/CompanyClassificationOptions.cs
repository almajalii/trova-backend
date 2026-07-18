namespace TrovaBackend.Services.CompanyDetails;

// Bound from the "CompanyClassification" section of appsettings.json.
// Change the numbers there and restart — no code changes needed to tune
// the thresholds. (A real admin-editable version of this would read from
// the database instead; this is the practical middle ground for now.)
public class CompanyClassificationOptions
{
    public ClassTierThresholds ClassA { get; set; } = new();
    public ClassTierThresholds ClassB { get; set; } = new();
}

public class ClassTierThresholds
{
    public int MinTeamSize { get; set; }
    public decimal MinRevenueJod { get; set; }
    public int MinYearsInOperation { get; set; }
}