using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.BankConnection;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.BankConnection;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/bank-connection")]
[Authorize]
public class BankConnectionController : ControllerBase
{
    private readonly IBankConnectionService _bankConnectionService;

    public BankConnectionController(IBankConnectionService bankConnectionService)
    {
        _bankConnectionService = bankConnectionService;
    }

    // GET /api/bank-connection/banks
    // Lets the frontend render the bank picker from real data instead of
    // hardcoding the 11 bank codes/names on its own — single source of
    // truth stays TrovaBanks.DisplayNames.
    [HttpGet("banks")]
    public IActionResult GetAvailableBanks()
    {
        var banks = TrovaBanks.DisplayNames
            .Select(kv => new BankOptionDto { Code = kv.Key, Name = kv.Value })
            .ToList();

        return Ok(new ApiResponse<List<BankOptionDto>>
        {
            Success = true,
            Message = "Available banks retrieved successfully.",
            Data = banks
        });
    }

    // POST /api/bank-connection/connect
    // Matches the Connect Bank -> Bank Consent Modal -> Authorize flow.
    // Immediately triggers a capability score recalculation server-side.
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectBankRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bankConnectionService.ConnectAsync(userId, request);
        return Ok(new ApiResponse<ConnectBankResponse>
        {
            Success = true,
            Message = "Bank connected successfully.",
            Data = result
        });
    }
}