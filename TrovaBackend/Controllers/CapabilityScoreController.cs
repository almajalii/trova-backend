using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.CapabilityScore;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.CapabilityScore;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/capability-score")]
[Authorize]
public class CapabilityScoreController : ControllerBase
{
    private readonly ICapabilityScoreService _capabilityScoreService;

    public CapabilityScoreController(ICapabilityScoreService capabilityScoreService)
    {
        _capabilityScoreService = capabilityScoreService;
    }

    // GET /api/capability-score/me
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _capabilityScoreService.GetAsync(userId);
        return Ok(new ApiResponse<CapabilityScoreResponse>
        {
            Success = true,
            Message = "Capability score retrieved successfully.",
            Data = result
        });
    }
}
