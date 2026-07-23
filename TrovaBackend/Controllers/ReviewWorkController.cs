using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.ReviewWork;
using TrovaBackend.Services.ReviewWork;

namespace TrovaBackend.Controllers;

// Owner-facing. Routes live under /api/projects/{projectId}/... to match
// review_work_service.dart exactly, even though this isn't ProjectsController
// — same "absolute route, separate domain" pattern as
// GuaranteesController.GetForProject.
[ApiController]
[Route("api/projects")]
[Authorize]
public class ReviewWorkController : ControllerBase
{
    private readonly IReviewWorkService _reviewWorkService;

    public ReviewWorkController(IReviewWorkService reviewWorkService)
    {
        _reviewWorkService = reviewWorkService;
    }

    // GET /api/projects/{projectId}/submitted-work
    [HttpGet("{projectId}/submitted-work")]
    public async Task<IActionResult> GetSubmittedWork(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _reviewWorkService.GetSubmittedWorkAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<SubmittedWorkDto?>
            {
                Success = false,
                Message = "No submitted work found for this project.",
                Data = null
            });
        }

        return Ok(new ApiResponse<SubmittedWorkDto>
        {
            Success = true,
            Message = "Submitted work retrieved successfully.",
            Data = result
        });
    }

    // POST /api/projects/{projectId}/confirm-complete
    [HttpPost("{projectId}/confirm-complete")]
    public async Task<IActionResult> ConfirmComplete(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _reviewWorkService.ConfirmCompleteAsync(ownerId, projectId);
        return Ok(new ApiResponse<object?>
        {
            Success = true,
            Message = "Project marked as completed.",
            Data = null
        });
    }

    // POST /api/projects/{projectId}/flag-issue
    [HttpPost("{projectId}/flag-issue")]
    public async Task<IActionResult> FlagIssue(string projectId, [FromBody] FlagIssueRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _reviewWorkService.FlagIssueAsync(ownerId, projectId, request.Reason);
        return Ok(new ApiResponse<object?>
        {
            Success = true,
            Message = "Project flagged as disputed.",
            Data = null
        });
    }
}
