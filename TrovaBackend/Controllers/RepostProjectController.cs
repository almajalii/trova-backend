using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.RepostProject;
using TrovaBackend.Services.RepostProject;

namespace TrovaBackend.Controllers;

// Owner-facing. Matches repost_project_service.dart's routes exactly —
// note the "/owner/projects/..." prefix, distinct from ProjectsController's
// plain "/projects/...".
[ApiController]
[Route("api/owner/projects")]
[Authorize]
public class RepostProjectController : ControllerBase
{
    private readonly IRepostProjectService _repostProjectService;

    public RepostProjectController(IRepostProjectService repostProjectService)
    {
        _repostProjectService = repostProjectService;
    }

    // GET /api/owner/projects/{projectId}/repost-draft
    [HttpGet("{projectId}/repost-draft")]
    public async Task<IActionResult> GetDraft(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _repostProjectService.GetDraftAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<RepostDraftDto?>
            {
                Success = false,
                Message = "No repostable project found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<RepostDraftDto>
        {
            Success = true,
            Message = "Repost draft retrieved successfully.",
            Data = result
        });
    }

    // POST /api/owner/projects/{projectId}/repost
    [HttpPost("{projectId}/repost")]
    public async Task<IActionResult> Submit(string projectId, [FromBody] RepostProjectRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _repostProjectService.SubmitRepostAsync(ownerId, projectId, request);
        return StatusCode(201, new ApiResponse<RepostProjectResponse>
        {
            Success = true,
            Message = "Project reposted successfully.",
            Data = result
        });
    }
}
