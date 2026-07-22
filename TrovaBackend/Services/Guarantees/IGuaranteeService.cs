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

    // Owner's decision on a pending application. Scoped to ownerId — only
    // the project's beneficiary can approve/reject their own guarantee.
    // Keyed by ApplicationCode (TRV-GT-XXXXX), the same public identifier
    // SubmitAsync returns.
    Task<OwnerGuaranteeDto> ApproveAsync(Guid ownerId, string applicationCode);
    Task<OwnerGuaranteeDto> RejectAsync(Guid ownerId, string applicationCode);
}
