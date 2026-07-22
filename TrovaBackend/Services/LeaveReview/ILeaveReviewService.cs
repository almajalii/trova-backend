using TrovaBackend.DTOs.LeaveReview;

namespace TrovaBackend.Services.LeaveReview;

public interface ILeaveReviewService
{
    // Null if the project doesn't exist, isn't the caller's, isn't
    // Completed yet, or has already been reviewed.
    Task<ReviewContextDto?> GetContextAsync(Guid ownerId, string projectId);

    // Throws KeyNotFoundException / InvalidOperationException for the
    // same reasons GetContextAsync would return null, plus ArgumentException
    // if any of the six categories is missing or out of the 1-5 range.
    Task SubmitReviewAsync(Guid ownerId, string projectId, SubmitReviewRequest request);
}
