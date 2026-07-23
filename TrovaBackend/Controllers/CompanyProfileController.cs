using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.CompanyProfile;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/company-profile")]
[Authorize]
public class CompanyProfileController : ControllerBase
{
    private readonly ICompanyProfileService _companyProfileService;

    public CompanyProfileController(ICompanyProfileService companyProfileService)
    {
        _companyProfileService = companyProfileService;
    }

    // GET /api/company-profile/reviews
    // Contractor's own Company Profile screen. Scoped to the calling
    // contractor via the bearer token — no route/query parameter, unlike
    // the bidder-facing GET /bids/{bidId}/company-profile.
    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews()
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _companyProfileService.GetReviewsAsync(contractorId);

        return Ok(new ApiResponse<BidderReviewsSummaryDto>
        {
            Success = true,
            Message = "Reviews retrieved successfully.",
            Data = result
        });
    }
}
