using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Projects;
using TrovaBackend.Services.Projects;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    // POST /api/projects
    // Owner-authenticated. Initial status is always OPEN_FOR_BIDS — not
    // settable from the request body.
    [HttpPost]
    public async Task<IActionResult> PostProject([FromBody] PostProjectRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.PostProjectAsync(ownerId, request);
        return StatusCode(201, new ApiResponse<PostProjectResponse>
        {
            Success = true,
            Message = "Project posted successfully.",
            Data = result
        });
    }

    // GET /api/projects/mine
    // Active projects only (Open for Bids / Awarded / Contractor Backed Off /
    // Guarantee Rejected by You / In Progress / Pending Review).
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.GetMyProjectsAsync(ownerId);
        return Ok(new ApiResponse<List<ProjectListItemDto>>
        {
            Success = true,
            Message = "Projects retrieved successfully.",
            Data = result
        });
    }

    // GET /api/projects/mine/history
    // Completed / Disputed / Failed only — separate list, not a filter on Mine().
    [HttpGet("mine/history")]
    public async Task<IActionResult> MineHistory()
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.GetMyProjectHistoryAsync(ownerId);
        return Ok(new ApiResponse<List<ProjectHistoryItemDto>>
        {
            Success = true,
            Message = "Project history retrieved successfully.",
            Data = result
        });
    }

    // GET /api/projects/{projectId}
    // Shared drill-in target for both Mine() and MineHistory(). Scoped to
    // the caller's own projects — a project that exists but belongs to
    // someone else 404s the same as one that doesn't exist at all.
    [HttpGet("{projectId}")]
    public async Task<IActionResult> Detail(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.GetProjectDetailAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<ProjectDetailDto?>
            {
                Success = false,
                Message = "Project not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<ProjectDetailDto>
        {
            Success = true,
            Message = "Project detail retrieved successfully.",
            Data = result
        });
    }

    // GET /api/projects/{projectId}/bids
    // Owner compares bidders for a project they own. Scores/classification
    // are always recomputed fresh from the same engine as
    // GET /api/capability-score/me — never cached/stale here.
    [HttpGet("{projectId}/bids")]
    public async Task<IActionResult> Bidders(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.GetProjectBiddersAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<List<BidderDto>?>
            {
                Success = false,
                Message = "Project not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<List<BidderDto>>
        {
            Success = true,
            Message = "Bidders retrieved successfully.",
            Data = result
        });
    }

    // POST /api/projects/{projectId}/award
    // Re-validates ownership, project state, and bid eligibility
    // server-side — the client's own eligible flag is never trusted.
    [HttpPost("{projectId}/award")]
    public async Task<IActionResult> Award(string projectId, [FromBody] AwardProjectRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.AwardProjectAsync(ownerId, projectId, request);

        if (result == null)
        {
            return NotFound(new ApiResponse<AwardProjectResponse?>
            {
                Success = false,
                Message = "Project not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<AwardProjectResponse>
        {
            Success = true,
            Message = "Project awarded successfully.",
            Data = result
        });
    }

    // GET /api/projects/browse?sectors=Construction&sectors=Industrial&minValue=100000&maxValue=500000
    // Contractor-facing. Only ever surfaces OPEN_FOR_BIDS projects — no
    // ownership scoping, any authenticated user can browse.
    [HttpGet("browse")]
    public async Task<IActionResult> Browse(
        [FromQuery] List<string>? sectors,
        [FromQuery] decimal? minValue,
        [FromQuery] decimal? maxValue)
    {
        var result = await _projectService.BrowseProjectsAsync(sectors, minValue, maxValue);
        return Ok(new ApiResponse<List<BrowseProjectListItemDto>>
        {
            Success = true,
            Message = "Projects retrieved successfully.",
            Data = result
        });
    }

    // GET /api/projects/browse/{projectId}
    // Contractor-facing detail — separate route from the owner-only
    // GET /{projectId} above (that one 404s for anyone but the owner).
    [HttpGet("browse/{projectId}")]
    public async Task<IActionResult> BrowseDetail(string projectId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.GetBrowseProjectDetailAsync(contractorId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<BrowseProjectDetailDto?>
            {
                Success = false,
                Message = "Project not found or no longer open for bids.",
                Data = null
            });
        }

        return Ok(new ApiResponse<BrowseProjectDetailDto>
        {
            Success = true,
            Message = "Project detail retrieved successfully.",
            Data = result
        });
    }

    // POST /api/projects/browse/{projectId}/bid
    [HttpPost("browse/{projectId}/bid")]
    public async Task<IActionResult> SubmitBid(string projectId, [FromBody] SubmitBidRequest request)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _projectService.SubmitBidAsync(contractorId, projectId, request);
        return StatusCode(201, new ApiResponse<SubmitBidResponse>
        {
            Success = true,
            Message = "Bid submitted successfully.",
            Data = result
        });
    }
}