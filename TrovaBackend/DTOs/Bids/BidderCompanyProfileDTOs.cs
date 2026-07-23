namespace TrovaBackend.DTOs.Bids;

// ── Bidder Company Profile — GET /api/bids/{bidId}/company-profile ─────────
// Matches bidder_profile_model.dart's BidderFullProfile.fromJson EXACTLY.
// Unlike every other DTO in this pass, the parser on the other end does
// strict, non-defensive casts (`as String`, `as int`, etc.) with no
// try/catch anywhere in the call chain — a missing field, a null where a
// string is expected, or a decimal where `scoreBreakdown` expects a whole
// int throws an uncaught TypeError on the client. So every property here
// is non-nullable and every builder method backing it must always
// populate a real value, never leave a default hanging.
//
// The one exception: TrackRecordStats.CurrentProjects is the single
// lenient field on the frontend (defaults to 0 if missing) — still always
// sent here for simplicity, just noting it's not do-or-die like the rest.
public class BidderCompanyProfileDto
{
    public string TradingName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxVatNumber { get; set; } = string.Empty;
    public string LegalStructure { get; set; } = string.Empty;
    public int YearOfEstablishment { get; set; }
    public string RegisteredAddress { get; set; } = string.Empty;
    public string CountryOfRegistration { get; set; } = string.Empty;
    public string PrimaryContactName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string PrimaryEmail { get; set; } = string.Empty;
    public string PrimaryPhoneNumber { get; set; } = string.Empty;
    public string BusinessLicenseNumber { get; set; } = string.Empty;
    public string ContractorClassificationGrade { get; set; } = string.Empty;
    public List<string> Sectors { get; set; } = new();

    // Not a stored column (see CompanyDetailsService's note on
    // YearsOfExperience being accepted-but-not-persisted) — derived here
    // as UtcNow.Year - YearOfEstablishment so the field is never missing.
    public int YearsOfExperience { get; set; }

    public BidderTrackRecordStatsDto TrackRecordStats { get; set; } = new();

    // Deliberately still sent even though the profile screen no longer
    // renders it — see the handoff note. Computed fresh below, not
    // hardcoded, so it stays meaningful if that UI section ever comes back.
    public BidderScoreBreakdownDto ScoreBreakdown { get; set; } = new();

    public BidderReviewsSummaryDto Reviews { get; set; } = new();

    // Deliberately NOT included: bank name / IBAN / SWIFT. Never shown to
    // a competing bidder or project owner — see the handoff note.
}

public class BidderTrackRecordStatsDto
{
    public int TotalProjects { get; set; }
    public int FailedProjects { get; set; }
    public int CurrentProjects { get; set; } // the one lenient field client-side
    public double AvgRating { get; set; }
}

// Three-category roll-up of the 8-factor capability score, computed here
// since no such 3-way grouping exists elsewhere in the codebase yet:
//   FinancialSolvency  = avg(current debts, debt capacity, assets value,
//                             delinquent debts, payment history, cashflow)
//   ProjectTrackRecord = avg(current workload, delivery history)
//   PastProjectRatings = avgRating scaled from a 0-5 star average to 0-100
// All three are whole numbers (Math.Round to int) — the frontend cast is
// `as int` and throws on a decimal, so half-scores are never sent.
public class BidderScoreBreakdownDto
{
    public int FinancialSolvency { get; set; }
    public int ProjectTrackRecord { get; set; }
    public int PastProjectRatings { get; set; }
}

public class BidderReviewsSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public List<BidderReviewItemDto> Items { get; set; } = new(); // [] when empty, never null
}

public class BidderReviewItemDto
{
    public string ReviewerName { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;

    // Rounded average of that single review's 6 rating categories.
    public int Stars { get; set; }

    public string Comment { get; set; } = string.Empty;
}
