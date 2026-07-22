namespace TrovaBackend.Models;

// Only one state exists in this pass — the bank-review workflow (approve /
// reject / active / expired / claimed) isn't modelled yet, same "build the
// table now, wire the rest later" approach as Bid/Project statuses.
public static class GuaranteeStatus
{
    public const string PendingBankReview = "pending_bank_review";
}

// Internal storage is SCREAMING_SNAKE, matching the external vocabulary
// convention used everywhere else (BidStatusMapper, Project.Status
// .ToUpperInvariant()). The frontend's Dart enum sends camelCase
// (`guaranteeType.name`) — GuaranteeService.ParseGuaranteeType is the
// single place that translates between the two. If camelCase turns
// out to be the preferred wire format end-to-end, delete the mapper and
// store the raw value instead — flagged here rather than decided silently.
public static class GuaranteeTypes
{
    public const string Performance = "PERFORMANCE";
    public const string BidBond = "BID_BOND";
    public const string AdvancePayment = "ADVANCE_PAYMENT";
    public const string Retention = "RETENTION";
}

public class GuaranteeApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Public-facing identifier, format TRV-GT-XXXXX — same pattern as
    // Project.ProjectCode.
    public string ApplicationCode { get; set; } = string.Empty;

    public Guid ContractorId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid BidId { get; set; }
    public Guid BeneficiaryId { get; set; } // the project owner

    public string GuaranteeType { get; set; } = string.Empty;
    public decimal GuaranteedAmount { get; set; }
    public string Currency { get; set; } = "JOD";

    public DateTime ValidityStart { get; set; }
    public DateTime ValidityExpiry { get; set; }

    public string? SpecialConditions { get; set; }

    // All three must be true to submit — enforced in GuaranteeService, not
    // just at the DTO layer, since these are compliance/legal gates.
    public bool ConfirmAccurate { get; set; }
    public bool AgreeIndemnify { get; set; }
    public bool AcceptTerms { get; set; }

    // Typed name, not a real e-signature — matches what the frontend
    // actually collects today.
    public string SignatureName { get; set; } = string.Empty;

    public string Status { get; set; } = GuaranteeStatus.PendingBankReview;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class GuaranteeDocumentType
{
    public const string SignedContract = "signed_contract";
    public const string LetterOfAward = "letter_of_award";
    public const string Other = "other";
}

public class GuaranteeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuaranteeApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty; // signed_contract | letter_of_award | other
    public string OriginalFileName { get; set; } = string.Empty;

    // Unique on-disk name (GUID-prefixed) — never trust the original file
    // name for the storage path, same reasoning as everywhere else in this
    // codebase that treats client input as untrusted.
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
