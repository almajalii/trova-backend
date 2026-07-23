using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Admin;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.Admin;

namespace TrovaBackend.Controllers;

// Admin-only. Every route requires the "admin" role — same
// [Authorize(Roles = "...")] pattern BankController uses for the bank
// portal. One admin account for this pass (seeded via migration), same as
// the single seeded bank account.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ── Whitelist (pending users) ───────────────────────────────────────

    // GET /api/admin/users/pending
    [HttpGet("users/pending")]
    public async Task<IActionResult> GetPendingUsers()
    {
        var result = await _adminService.GetPendingUsersAsync();
        return Ok(new ApiResponse<List<AdminPendingUserDto>>
        {
            Success = true,
            Message = "Pending users retrieved successfully.",
            Data = result
        });
    }

    // POST /api/admin/users/{id}/approve
    [HttpPost("users/{id}/approve")]
    public async Task<IActionResult> ApproveUser(Guid id)
    {
        await _adminService.ApproveUserAsync(id);
        return Ok(new ApiResponse<object?>
        {
            Success = true,
            Message = "User approved.",
            Data = null
        });
    }

    // POST /api/admin/users/{id}/reject
    [HttpPost("users/{id}/reject")]
    public async Task<IActionResult> RejectUser(Guid id, [FromBody] AdminRejectUserRequest request)
    {
        await _adminService.RejectUserAsync(id, request.Reason);
        return Ok(new ApiResponse<object?>
        {
            Success = true,
            Message = "User rejected.",
            Data = null
        });
    }

    // ── Users ────────────────────────────────────────────────────────────

    // GET /api/admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _adminService.GetUsersAsync();
        return Ok(new ApiResponse<List<AdminUserSummaryDto>>
        {
            Success = true,
            Message = "Users retrieved successfully.",
            Data = result
        });
    }

    // ── Disputes ─────────────────────────────────────────────────────────

    // GET /api/admin/disputes
    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes()
    {
        var result = await _adminService.GetDisputesAsync();
        return Ok(new ApiResponse<List<AdminDisputeSummaryDto>>
        {
            Success = true,
            Message = "Disputes retrieved successfully.",
            Data = result
        });
    }

    // GET /api/admin/disputes/{projectId}
    [HttpGet("disputes/{projectId}")]
    public async Task<IActionResult> GetDispute(string projectId)
    {
        var result = await _adminService.GetDisputeAsync(projectId);
        if (result == null)
        {
            return NotFound(new ApiResponse<AdminDisputeDetailDto?>
            {
                Success = false,
                Message = "No dispute found for this project.",
                Data = null
            });
        }

        return Ok(new ApiResponse<AdminDisputeDetailDto>
        {
            Success = true,
            Message = "Dispute retrieved successfully.",
            Data = result
        });
    }

    // POST /api/admin/disputes/{projectId}/resolve
    [HttpPost("disputes/{projectId}/resolve")]
    public async Task<IActionResult> ResolveDispute(string projectId, [FromBody] AdminResolveDisputeRequest request)
    {
        await _adminService.ResolveDisputeAsync(projectId, request.Message);
        return Ok(new ApiResponse<object?>
        {
            Success = true,
            Message = "Dispute resolved.",
            Data = null
        });
    }
}
