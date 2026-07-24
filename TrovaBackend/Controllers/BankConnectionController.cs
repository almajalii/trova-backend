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
    // hardcoding bank codes/names on its own. Full source of truth for
    // valid bank codes stays TrovaBanks.DisplayNames (still used as-is by
    // BankConnectionService.ConnectAsync for validation) - this endpoint
    // deliberately only surfaces a curated subset of it for the picker,
    // currently Housing Bank / Capital Bank / Jordan Kuwait Bank. Update
    // EnabledBankCodes below to change which banks show up here; codes
    // removed from this list still work if a client somehow sends them to
    // /connect directly, they just won't be offered in the picker.
    private static readonly string[] EnabledBankCodes =
    {
        TrovaBanks.HousingBank, TrovaBanks.CapitalBank, TrovaBanks.JordanKuwaitBank
    };

    [HttpGet("banks")]
    public IActionResult GetAvailableBanks()
    {
        var banks = EnabledBankCodes
            .Select(code => new BankOptionDto { Code = code, Name = TrovaBanks.DisplayNames[code] })
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