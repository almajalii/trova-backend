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
}