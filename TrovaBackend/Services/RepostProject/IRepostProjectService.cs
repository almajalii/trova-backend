using TrovaBackend.DTOs.RepostProject;

namespace TrovaBackend.Services.RepostProject;

public interface IRepostProjectService
{
    // Null if the project doesn't exist, isn't the caller's, or isn't in
    // a repostable state (ContractorBackedOff / GuaranteeRejectedByYou).
    Task<RepostDraftDto?> GetDraftAsync(Guid ownerId, string projectId);

    // Creates a brand-new Project from the (possibly edited) draft and
    // marks the original Cancelled so it drops out of the active list.
    // Throws KeyNotFoundException / InvalidOperationException for the
    // same reasons GetDraftAsync would return null.
    Task<RepostProjectResponse> SubmitRepostAsync(Guid ownerId, string projectId, RepostProjectRequest request);
}
