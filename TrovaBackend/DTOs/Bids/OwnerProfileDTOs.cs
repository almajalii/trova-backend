namespace TrovaBackend.DTOs.Bids;

// ── Project Owner Profile — GET /api/bids/{bidId}/owner-profile ────────────
// Contractor-facing mirror of BidderCompanyProfileDto (which is
// owner-facing). Scoped by bidId the same way: only the contractor who
// placed that specific bid can resolve the project owner's profile
// through it, so a bid that exists but wasn't placed by the caller 404s
// the same as a nonexistent one — see BidService.GetOwnerProfileAsync.
//
// Deliberately excludes anything tied to contractor bid-eligibility
// grading (no classification/grade field — that's a property of
// contractors, not owners) and excludes a reviews list, since there's no
// flow yet for a contractor to review a project owner.
public class OwnerProfileDto
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

    // Same "not a stored column, derived as UtcNow.Year - YearOfEstablishment"
    // pattern as BidderCompanyProfileDto.YearsOfExperience.
    public int YearsOfExperience { get; set; }

    // Distinct sectors across every project this owner has posted —
    // analogous to a contractor's declared Sectors, but derived from
    // actual Project rows rather than a stored CompanyDetails field,
    // since "sectors posted" is a fact about their project history, not
    // something they self-declare.
    public List<string> SectorsPosted { get; set; } = new();

    public OwnerTrackRecordStatsDto TrackRecordStats { get; set; } = new();
}

public class OwnerTrackRecordStatsDto
{
    public int TotalProjectsPosted { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }

    // NOTE: always 0.0 for now — there is no schema path for a contractor
    // reviewing a project owner (Review.ReviewerId is always the owner,
    // RevieweeId is always the contractor; see Models/Review.cs). Sent as
    // a real 0.0 rather than omitted, since the frontend spec marks this
    // field required, but this is a placeholder until that review flow
    // exists, not a computed average.
    public double AvgRating { get; set; }
}