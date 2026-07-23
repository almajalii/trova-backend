namespace TrovaBackend.Models;

// Two-stage review workflow:
//   pending_bank_review -> issued -> approved   (bank issues, then owner confirms)
//                        \-> rejected             (either the bank or the owner can
//   issued              -/                        reject — see GuaranteeRejectedBy)
//
// "Approved" is deliberately still the terminal success value (not renamed)
// so BidService/ProjectService/ReviewWorkService's existing
// "GuaranteeStatus.Approved -> bid/project move to InProgress" checks keep
// working unchanged — that transition now fires from the owner's Confirm
// step instead of the bank's Issue step, but the status value it lands on
// is the same one those checks already look for.
public static class GuaranteeStatus
{
    public const string PendingBankReview = "pending_bank_review";
    public const string Issued = "issued"; // bank approved; awaiting the owner's confirmation
    public const string Approved = "approved"; // owner confirmed — terminal success
    public const string Rejected = "rejected"; // rejected by either side — see RejectedBy/RejectionReason
}

public static class GuaranteeRejectedBy
{
    public const string Bank = "bank";
    public const string Owner = "owner";
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

    // Set when the bank issues the guarantee (Status -> Issued). Distinct
    // from UpdatedAt because UpdatedAt keeps moving (e.g. once the owner
    // later confirms/rejects), but "when did the bank issue this" needs to
    // stay fixed once set — the bank portal's "Issued {date}" label reads
    // this, not UpdatedAt.
    public DateTime? IssuedAt { get; set; }

    // Set only when Status == Rejected. Bank rejections always populate
    // this (required at the API layer); owner rejections may leave it
    // null since the owner-facing UI doesn't collect one today.
    public string? RejectionReason { get; set; }

    // "bank" | "owner" — see GuaranteeRejectedBy. Null unless Status ==
    // Rejected. Lets the contractor's UI show who rejected it, not just
    // that it was rejected.
    public string? RejectedBy { get; set; }

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
