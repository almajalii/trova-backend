using TrovaBackend.DTOs.Guarantees;

namespace TrovaBackend.Services.Guarantees;

public interface IGuaranteeService
{
    Task<GuaranteePrefillResponse> GetPrefillAsync(Guid contractorId, string projectId);
    Task<SubmitGuaranteeResponse> SubmitAsync(Guid contractorId, SubmitGuaranteeRequest request);

    // Owner-facing read of the latest guarantee application on a project
    // they own. Null if the project doesn't exist, isn't theirs, or no
    // application has been submitted for it yet (all three 404 the same
    // way, same pattern as ProjectService.GetProjectDetailAsync).
    Task<OwnerGuaranteeDto?> GetOwnerGuaranteeAsync(Guid ownerId, string projectId);

    // Owner's decision on a guarantee the bank has already issued (Status
    // == Issued). Scoped to ownerId — only the project's beneficiary can
    // confirm/reject. Keyed by ProjectId (matches GetOwnerGuaranteeAsync's
    // access pattern — the owner's screen only ever knows the projectId,
    // not the application's code).
    Task<OwnerGuaranteeDto> ConfirmAsync(Guid ownerId, string projectId);
    Task<OwnerGuaranteeDto> RejectByOwnerAsync(Guid ownerId, string projectId, string? reason);

    // ── Bank-facing ──────────────────────────────────────────────────────
    // One bank account sees every application — no per-bank scoping.

    Task<List<BankGuaranteeDto>> GetBankRequestsAsync(); // pending_bank_review queue
    Task<List<BankGuaranteeDto>> GetBankGuaranteesAsync(); // issued + approved

    // Bank's decision on a pending application. Keyed by ApplicationCode
    // (TRV-GT-XXXXX), the same public identifier SubmitAsync returns.
    Task<BankGuaranteeDto> IssueAsync(string applicationCode);
    Task<BankGuaranteeDto> RejectByBankAsync(string applicationCode, string reason);
}
