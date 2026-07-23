using TrovaBackend.DTOs.ReviewWork;

namespace TrovaBackend.Services.ReviewWork;

public interface IReviewWorkService
{
    // Null if the project doesn't exist, isn't the caller's, or hasn't
    // had work submitted for it yet (all three 404 the same way).
    Task<SubmittedWorkDto?> GetSubmittedWorkAsync(Guid ownerId, string projectId);

    // Both throw KeyNotFoundException if the project isn't the caller's,
    // and InvalidOperationException if it isn't in PendingReview.
    Task ConfirmCompleteAsync(Guid ownerId, string projectId);

    // reason is the owner's explanation for disputing the work — stored on
    // Project.DisputeReason, shown to the admin resolving it.
    Task FlagIssueAsync(Guid ownerId, string projectId, string reason);
}
