using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.CompanyDetails;
using TrovaBackend.Services.CompanyDetails;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/company-details")]
[Authorize]
public class CompanyDetailsController : ControllerBase
{
    private readonly ICompanyDetailsService _companyDetailsService;

    public CompanyDetailsController(ICompanyDetailsService companyDetailsService)
    {
        _companyDetailsService = companyDetailsService;
    }

    // POST /api/company-details
    // Matches Flutter's CompanyDetailsService.submit — called once from the
    // onboarding step between identity verification and Connect Bank.
    // Also acts as an upsert if called again (e.g. user edits from Company
    // Profile later), same pattern the Flutter comment expects.
    [HttpPost("")]
    public async Task<IActionResult> Submit([FromBody] SubmitCompanyDetailsRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _companyDetailsService.SubmitAsync(userId, request);
        return StatusCode(201, new ApiResponse<CompanyDetailsResponse>
        {
            Success = true,
            Message = "Company details saved successfully.",
            Data = result
        });
    }

    // GET /api/company-details
    // Used by Company Profile / My Score screens to display the saved
    // company info and classification without resubmitting the form.
    [HttpGet("")]
    public async Task<IActionResult> Get()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _companyDetailsService.GetAsync(userId);
        return Ok(new ApiResponse<CompanyDetailsFullResponse>
        {
            Success = true,
            Message = "Company details retrieved successfully.",
            Data = result
        });
    }
}