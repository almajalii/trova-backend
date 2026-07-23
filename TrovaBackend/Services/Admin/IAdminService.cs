using TrovaBackend.DTOs.Admin;

namespace TrovaBackend.Services.Admin;

public interface IAdminService
{
    // ── Whitelist (pending users) ───────────────────────────────────────
    Task<List<AdminPendingUserDto>> GetPendingUsersAsync();

    // Throws KeyNotFoundException if userId doesn't exist, InvalidOperationException
    // if the user isn't Pending (already decided).
    Task ApproveUserAsync(Guid userId);
    Task RejectUserAsync(Guid userId, string reason);

    // ── Users ────────────────────────────────────────────────────────────
    Task<List<AdminUserSummaryDto>> GetUsersAsync();

    // ── Disputes ─────────────────────────────────────────────────────────
    Task<List<AdminDisputeSummaryDto>> GetDisputesAsync();

    // Null if no project with that code has ever been disputed.
    Task<AdminDisputeDetailDto?> GetDisputeAsync(string projectId);

    // Throws KeyNotFoundException if projectId doesn't exist / was never
    // disputed, InvalidOperationException if it's already resolved.
    Task ResolveDisputeAsync(string projectId, string message);
}
