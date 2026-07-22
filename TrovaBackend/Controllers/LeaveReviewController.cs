using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.LeaveReview;
using TrovaBackend.Services.LeaveReview;

namespace TrovaBackend.Controllers;

// Owner-facing. Matches leave_review_service.dart's routes exactly —
// same "/owner/projects/..." prefix as RepostProjectController.
[ApiController]
[Route("api/owner/projects")]
[Authorize]
public class LeaveReviewController : ControllerBase
{
    private readonly ILeaveReviewService _leaveReviewService;

    public LeaveReviewController(ILeaveReviewService leaveReviewService)
    {
        _leaveReviewService = leaveReviewService;
    }

    // GET /api/owner/projects/{projectId}/review-context
    [HttpGet("{projectId}/review-context")]
    public async Task<IActionResult> GetContext(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _leaveReviewService.GetContextAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<ReviewContextDto?>
            {
                Success = false,
                Message = "No reviewable project found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<ReviewContextDto>
        {
            Success = true,
            Message = "Review context retrieved successfully.",
            Data = result
        });
    }

    // POST /api/owner/projects/{projectId}/review
    [HttpPost("{projectId}/review")]
    public async Task<IActionResult> Submit(string projectId, [FromBody] SubmitReviewRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _leaveReviewService.SubmitReviewAsync(ownerId, projectId, request);
        return StatusCode(201, new ApiResponse<object?>
        {
            Success = true,
            Message = "Review submitted successfully.",
            Data = null
        });
    }
}
