namespace TrovaBackend.Models;

// Keep this exactly as you had it!
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
    public Guid UserId { get; set; }

    // New fields to match your Flutter DTO
    public string LegalCompanyName { get; set; } = string.Empty;
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

    // Sectors field
    public List<string> Sectors { get; set; } = new();

    public int YearsOfExperience { get; set; }
    public int TeamSize { get; set; }
    public decimal AnnualRevenueJod { get; set; }

    // ── Banking basics ──────────────────────────────────────────────────
    // Self-reported at company-details submission time — distinct from the
    // separate BankConnection/JOFS flow (Connect Bank), which is a later,
    // optional step and covers different fields (balances, debts, etc).
    // These four were previously accepted by CompanyDetailsDraftDto but
    // silently dropped; now persisted for real, matching the Dart contract.
    public string PrimaryBankName { get; set; } = string.Empty;
    public string IbanNumber { get; set; } = string.Empty;
    public string SwiftBicCode { get; set; } = string.Empty;
    public string BankBranchNameCity { get; set; } = string.Empty;

    // Classification
    public string ClassificationCode { get; set; } = string.Empty;
    public string ClassificationLabel { get; set; } = string.Empty;

    // 4-digit code generated once on first use (same "random, verify
    // uniqueness, persist" pattern as Project.ProjectCode) and reused
    // afterwards. Displayed as "TRV-CON-{code}" or "TRV-OWN-{code}"
    // depending on whether this company is being shown as a contractor or
    // a project owner in a given response — see
    // GuaranteeService.GetOrCreatePublicCodeAsync. Null until first needed.
    public string? PublicCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}