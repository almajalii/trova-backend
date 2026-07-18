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
