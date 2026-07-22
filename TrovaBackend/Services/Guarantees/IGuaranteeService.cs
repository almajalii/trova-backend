using TrovaBackend.DTOs.Guarantees;

namespace TrovaBackend.Services.Guarantees;

public interface IGuaranteeService
{
    Task<GuaranteePrefillResponse> GetPrefillAsync(Guid contractorId, string projectId);
    Task<SubmitGuaranteeResponse> SubmitAsync(Guid contractorId, SubmitGuaranteeRequest request);
}
