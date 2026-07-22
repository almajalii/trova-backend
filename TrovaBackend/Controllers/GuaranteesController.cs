using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Guarantees;
using TrovaBackend.Services.Guarantees;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/guarantees")]
[Authorize]
public class GuaranteesController : ControllerBase
{
    private readonly IGuaranteeService _guaranteeService;

    public GuaranteesController(IGuaranteeService guaranteeService)
    {
        _guaranteeService = guaranteeService;
    }

    // GET /api/guarantees/prefill?projectId={projectId}
    // Contractor-facing. Scoped to a project the caller has a confirmed
    // bid on — see GuaranteeService.ResolveConfirmedBidAsync.
    [HttpGet("prefill")]
    public async Task<IActionResult> Prefill([FromQuery] string projectId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _guaranteeService.GetPrefillAsync(contractorId, projectId);
        return Ok(new ApiResponse<GuaranteePrefillResponse>
        {
            Success = true,
            Message = "Guarantee prefill data retrieved successfully.",
            Data = result
        });
    }

    // POST /api/guarantees (multipart/form-data)
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Submit([FromForm] SubmitGuaranteeRequest request)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _guaranteeService.SubmitAsync(contractorId, request);
        return StatusCode(201, new ApiResponse<SubmitGuaranteeResponse>
        {
            Success = true,
            Message = "Guarantee application submitted successfully.",
            Data = result
        });
    }
}
