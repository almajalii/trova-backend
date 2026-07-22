namespace TrovaBackend.DTOs.Guarantees;

// ── Prefill — GET /api/guarantees/prefill?projectId={projectId} ────────────
public class GuaranteePrefillResponse
{
    // Step 1 - Applicant (the calling contractor's own company). Identical
    // field set to CompanyDetailsRecordDto's core fields — reuse
    // GET /api/company-details on the frontend if you'd rather not
    // duplicate a second copy of this data client-side.
    public string ContractorId { get; set; } = string.Empty;
    public string LegalCompanyName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxVatNumber { get; set; } = string.Empty;
    public string RegisteredAddress { get; set; } = string.Empty;
    public string PrimaryContact { get; set; } = string.Empty;
    public string PrimaryEmail { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;

    // Step 2 - Project (from the contractor's own confirmed bid on this project)
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ContractDuration { get; set; } = string.Empty;

    // Step 4 - Beneficiary (the project owner's company). Only ever
    // returned scoped to a project this contractor has a confirmed bid on.
    public string BeneficiaryId { get; set; } = string.Empty;
    public string BeneficiaryCompanyName { get; set; } = string.Empty;
    public string BeneficiaryAddress { get; set; } = string.Empty;
    public string BeneficiaryContact { get; set; } = string.Empty;
    public string BeneficiaryEmail { get; set; } = string.Empty;
    public string BeneficiaryPhone { get; set; } = string.Empty;
}

// ── Submit — POST /api/guarantees (multipart/form-data) ────────────────────
// Field names/types match guarantee_service.dart:12-26 exactly so the
// frontend needs zero rework. Amount/dates/booleans arrive as strings by
// design (that's what the client sends today) and are parsed/validated in
// GuaranteeService, not here — keeping this a plain bag of what was
// actually posted makes multipart model binding predictable.
public class SubmitGuaranteeRequest
{
    // Accepted but not trusted — the contractor is always taken from the
    // bearer token, never from the body. Same pattern as every other
    // "don't trust a client-supplied identity field" spot in this codebase.
    public string? ContractorId { get; set; }

    // Optional on the wire today; GuaranteeService requires it in
    // practice (needed to resolve the project + beneficiary + confirmed
    // bid) and throws a clear 400 if it's missing, rather than silently
    // accepting an unlinked guarantee application. Flip this to
    // [Required] once the frontend always sends it.
    public string? ProjectId { get; set; }

    // performance | bidBond | advancePayment | retention (camelCase, as
    // sent by the Dart enum's .name).
    public string GuaranteeType { get; set; } = string.Empty;

    public string GuaranteedAmount { get; set; } = string.Empty; // stringified decimal
    public string Currency { get; set; } = "JOD";

    public string ValidityStart { get; set; } = string.Empty; // ISO 8601
    public string ValidityExpiry { get; set; } = string.Empty; // ISO 8601

    public string? SpecialConditions { get; set; }

    // Accepted but not trusted, same as ContractorId — the real
    // beneficiary is always resolved from the project's owner.
    public string? BeneficiaryId { get; set; }

    public string ConfirmAccurate { get; set; } = "false"; // "true"/"false"
    public string AgreeIndemnify { get; set; } = "false";  // "true"/"false"
    public string AcceptTerms { get; set; } = "false";     // "true"/"false"

    public string SignatureName { get; set; } = string.Empty;

    public IFormFile? SignedContract { get; set; }
    public IFormFile? LetterOfAward { get; set; }
    public List<IFormFile>? OtherDocuments { get; set; }
}

public class SubmitGuaranteeResponse
{
    public string GuaranteeApplicationId { get; set; } = string.Empty; // TRV-GT-XXXXX
    public string Status { get; set; } = string.Empty; // PENDING_BANK_REVIEW
}

// ── Owner-side read/decision ────────────────────────────────────────────
// GET  /api/projects/{projectId}/guarantee
// POST /api/guarantees/{guaranteeId}/approve  → same shape, Status ACTIVE
// POST /api/guarantees/{guaranteeId}/reject   → same shape, Status REJECTED
//
// Matches guarantee_review_model.dart's OwnerGuarantee.fromJson exactly.
// This is the project owner reviewing a guarantee the contractor has
// already submitted — distinct from the contractor-facing Submit/Prefill
// DTOs above. "PENDING_REVIEW" here means "awaiting the owner's decision",
// which is what GuaranteeStatus.PendingBankReview means in practice today
// since no separate bank-side step exists yet (see the comment on
// GuaranteeService's Decision region) — internal storage keeps its
// existing name, only the external vocabulary differs.
public class OwnerGuaranteeDto
{
    public string GuaranteeId { get; set; } = string.Empty; // TRV-GT-XXXXX
    public string ProjectId { get; set; } = string.Empty; // TRV-PRJ-XXXXX
    public string ProjectTitle { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty; // "Principal" on the document
    public string Beneficiary { get; set; } = string.Empty; // owner's own company name + " (You)"
    public string IssuingBank { get; set; } = string.Empty;
    public decimal AmountJod { get; set; }
    public string Type { get; set; } = string.Empty; // e.g. "Performance Guarantee"
    public string Status { get; set; } = string.Empty; // PENDING_REVIEW | ACTIVE | REJECTED | CLAIMED
    public string? IssueDate { get; set; } // "yyyy-MM-dd", set once ACTIVE
    public string? ValidUntil { get; set; } // "yyyy-MM-dd"
    public string? ClaimDate { get; set; } // not modelled yet — always null until CLAIMED exists
}
