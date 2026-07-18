namespace TrovaBackend.Models;

// Canonical sector values — matches the Figma filter chips exactly
// (Browse Projects sector chips + Filter Projects Sheet). Single source
// of truth so CompanyDetails and, later, Post-a-Project / Browse Projects
// filtering all agree on the same set of strings.
public static class TrovaSectors
{
    public const string Construction = "Construction";
    public const string RealEstate = "Real Estate";
    public const string Infrastructure = "Infrastructure";
    public const string Industrial = "Industrial";
    public const string Mep = "MEP";
    public const string RenovationAndFitOut = "Renovation & Fit-out";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Construction, RealEstate, Infrastructure, Industrial, Mep, RenovationAndFitOut
    };
}

public class CompanyDetails
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // One company profile per user. No navigation property to User here,
    // same minimal style as the rest of the domain so far — join in the
    // service layer with a plain query if you ever need both together.
    public Guid UserId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    // A company can work across multiple sectors (e.g. Construction +
    // Infrastructure) — mapped to a native Postgres text[] column by the
    // Npgsql EF provider, no join table needed.
    public List<string> Sectors { get; set; } = new();

    public string RegistrationNumber { get; set; } = string.Empty;
    public int YearsInOperation { get; set; }
    public int TeamSize { get; set; }
    public decimal AnnualRevenueJod { get; set; }

    // ── Classification (computed on submit, stored so it's cheap to read
    //    everywhere else it's displayed — Home Dashboard, My Score, Bidders
    //    List, Company Profile — without recalculating) ──────────────────
    public string ClassificationCode { get; set; } = string.Empty;  // "A" | "B" | "C"
    public string ClassificationLabel { get; set; } = string.Empty; // e.g. "Large Enterprise"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}