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

        // Recalculate-then-read — same pattern ProjectService.BuildBidderDtoAsync
        // and BidService's company-profile endpoint already use. Without this,
        // a contractor's own score view only reflected whatever was last saved
        // by a bank (re)connect or by an owner happening to view their bid —
        // finishing a project wouldn't show up here until one of those
        // unrelated events also happened to fire.
        await _capabilityScoreService.RecalculateAsync(userId);
        var result = await _capabilityScoreService.GetAsync(userId);
        return Ok(new ApiResponse<CapabilityScoreResponse>
        {
            Success = true,
            Message = "Capability score retrieved successfully.",
            Data = result
        });
    }
}
