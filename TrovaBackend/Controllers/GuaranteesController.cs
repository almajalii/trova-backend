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

    // GET /api/projects/{projectId}/guarantee
    // Owner-facing. Lives here (not ProjectsController) despite the URL,
    // since it's the guarantee-domain read — matches
    // guarantee_review_service.dart's fetchGuarantee() exactly. The
    // leading "/" makes this an absolute route, overriding the
    // controller-level "api/guarantees" prefix.
    [HttpGet("/api/projects/{projectId}/guarantee")]
    public async Task<IActionResult> GetForProject(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _guaranteeService.GetOwnerGuaranteeAsync(ownerId, projectId);

        if (result == null)
        {
            return NotFound(new ApiResponse<OwnerGuaranteeDto?>
            {
                Success = false,
                Message = "No guarantee found for this project.",
                Data = null
            });
        }

        return Ok(new ApiResponse<OwnerGuaranteeDto>
        {
            Success = true,
            Message = "Guarantee retrieved successfully.",
            Data = result
        });
    }

    // POST /api/guarantees/{applicationCode}/approve
    // POST /api/guarantees/{applicationCode}/reject
    //
    // Bank-facing. This is the bank issuing (or rejecting) the guarantee
    // the contractor applied for — the first of the two decisions in the
    // flow. Matches the bank portal's requests.js approveRequest()/
    // denyRequest(). The owner's second decision (confirming or rejecting
    // what the bank issued) lives below, under /api/projects/{id}/guarantee.
    [HttpPost("{applicationCode}/approve")]
    [Authorize(Roles = "bank")]
    public async Task<IActionResult> Approve(string applicationCode)
    {
        var result = await _guaranteeService.IssueAsync(applicationCode);
        return Ok(new ApiResponse<BankGuaranteeDto>
        {
            Success = true,
            Message = "Guarantee issued.",
            Data = result
        });
    }

    [HttpPost("{applicationCode}/reject")]
    [Authorize(Roles = "bank")]
    public async Task<IActionResult> Reject(string applicationCode, [FromBody] RejectGuaranteeRequest request)
    {
        var result = await _guaranteeService.RejectByBankAsync(applicationCode, request.Reason ?? string.Empty);
        return Ok(new ApiResponse<BankGuaranteeDto>
        {
            Success = true,
            Message = "Guarantee rejected.",
            Data = result
        });
    }

    // POST /api/projects/{projectId}/guarantee/confirm
    // POST /api/projects/{projectId}/guarantee/reject
    //
    // Owner-facing. This is the project owner's decision on what the
    // bank issued — scoped to ownerId in the service, so a caller who
    // isn't that project's beneficiary gets a 404, same as any other
    // project resource that isn't theirs. Matches
    // guarantee_review_service.dart's approveGuarantee()/rejectGuarantee()
    // (renamed confirm/reject here since "approve" is now the bank's verb).
    [HttpPost("/api/projects/{projectId}/guarantee/confirm")]
    public async Task<IActionResult> Confirm(string projectId)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _guaranteeService.ConfirmAsync(ownerId, projectId);
        return Ok(new ApiResponse<OwnerGuaranteeDto>
        {
            Success = true,
            Message = "Guarantee confirmed.",
            Data = result
        });
    }

    [HttpPost("/api/projects/{projectId}/guarantee/reject")]
    public async Task<IActionResult> RejectByOwner(string projectId, [FromBody] RejectGuaranteeRequest? request)
    {
        var ownerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _guaranteeService.RejectByOwnerAsync(ownerId, projectId, request?.Reason);
        return Ok(new ApiResponse<OwnerGuaranteeDto>
        {
            Success = true,
            Message = "Guarantee rejected.",
            Data = result
        });
    }
}
