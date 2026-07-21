using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.Bids;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/bids")]
[Authorize]
public class BidsController : ControllerBase
{
    private readonly IBidService _bidService;

    public BidsController(IBidService bidService)
    {
        _bidService = bidService;
    }

    // POST /api/bids/{bidId}/confirm
    // Contractor confirms a bid the owner awarded to them.
    [HttpPost("{bidId}/confirm")]
    public async Task<IActionResult> Confirm(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.ConfirmBidAsync(contractorId, bidId);

        if (result == null)
        {
            return NotFound(new ApiResponse<BidActionResponse?>
            {
                Success = false,
                Message = "Bid not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<BidActionResponse>
        {
            Success = true,
            Message = "Bid confirmed successfully.",
            Data = result
        });
    }

    // POST /api/bids/{bidId}/back-off
    // Contractor declines a bid the owner awarded to them.
    [HttpPost("{bidId}/back-off")]
    public async Task<IActionResult> BackOff(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.BackOffBidAsync(contractorId, bidId);

        if (result == null)
        {
            return NotFound(new ApiResponse<BidActionResponse?>
            {
                Success = false,
                Message = "Bid not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<BidActionResponse>
        {
            Success = true,
            Message = "You've backed off this bid.",
            Data = result
        });
    }
}
