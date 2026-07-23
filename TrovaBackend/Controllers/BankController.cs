using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Guarantees;
using TrovaBackend.Services.Guarantees;

namespace TrovaBackend.Controllers;

// Bank-portal list endpoints. The actual decision endpoints (approve/reject
// a single application) live on GuaranteesController — they were already
// there, keyed by applicationCode, so this controller only adds what's new:
// the two list views the bank portal's Requests and Active Guarantees
// screens read from. One bank account sees every application; there's no
// per-bank scoping (see the comment on IGuaranteeService).
[ApiController]
[Route("api/bank")]
[Authorize(Roles = "bank")]
public class BankController : ControllerBase
{
    private readonly IGuaranteeService _guaranteeService;

    public BankController(IGuaranteeService guaranteeService)
    {
        _guaranteeService = guaranteeService;
    }

    // GET /api/bank/requests — pending_bank_review queue.
    // Matches the bank portal's requests.js fetchRequests().
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests()
    {
        var result = await _guaranteeService.GetBankRequestsAsync();
        return Ok(new ApiResponse<List<BankGuaranteeDto>>
        {
            Success = true,
            Message = "Pending guarantee requests retrieved successfully.",
            Data = result
        });
    }

    // GET /api/bank/guarantees — issued + confirmed guarantees.
    // Matches the bank portal's guarantees.js fetchActiveGuarantees().
    [HttpGet("guarantees")]
    public async Task<IActionResult> GetGuarantees()
    {
        var result = await _guaranteeService.GetBankGuaranteesAsync();
        return Ok(new ApiResponse<List<BankGuaranteeDto>>
        {
            Success = true,
            Message = "Active guarantees retrieved successfully.",
            Data = result
        });
    }
}
