using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrovaBackend.DTOs;
using TrovaBackend.Services;

namespace TrovaBackend.Controllers
{
    [ApiController]
    [Route("api/company-details")]
    [Authorize] // Enforces the Bearer auth requirement
    public class CompanyDetailsController : ControllerBase
    {
        private readonly ICompanyDetailsService _companyDetailsService;

        public CompanyDetailsController(ICompanyDetailsService companyDetailsService)
        {
            _companyDetailsService = companyDetailsService;
        }

        // POST /api/company-details
        [HttpPost]
        public async Task<IActionResult> SubmitDetails([FromBody] CompanyDetailsDraftDto draft)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Extract the user ID from the Bearer token claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var classification = await _companyDetailsService.SubmitCompanyDetailsAsync(userId, draft);

            // Shape the response exactly as expected: { "data": { "classification": { "code": "A", "label": "..." } } }
            var response = new ApiResponse<object>
            {
                Data = new { Classification = classification }
            };

            return StatusCode(201, response);
        }

        // GET /api/company-details
        [HttpGet]
        public async Task<IActionResult> GetDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var record = await _companyDetailsService.GetCompanyDetailsAsync(userId);

            // Returns 404 (data: null) if the user hasn't submitted yet
            if (record == null)
            {
                return NotFound(new ApiResponse<object?> { Data = null });
            }

            return Ok(new ApiResponse<CompanyDetailsRecordDto> { Data = record });
        }
    }
}