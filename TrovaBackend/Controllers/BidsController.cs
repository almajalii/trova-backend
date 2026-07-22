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

    // GET /api/bids/mine
    // Contractor's active bids only — Pending/Selected/Confirmed/InProgress.
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.GetMyBidsAsync(contractorId);
        return Ok(new ApiResponse<List<MyBidItemDto>>
        {
            Success = true,
            Message = "Bids retrieved successfully.",
            Data = result
        });
    }

    // GET /api/bids/history
    // Contractor's closed bids only — Completed/Rejected/Withdrawn.
    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.GetHistoryAsync(contractorId);
        return Ok(new ApiResponse<List<BidHistoryItemDto>>
        {
            Success = true,
            Message = "Bid history retrieved successfully.",
            Data = result
        });
    }

    // POST /api/bids/{bidId}/confirm
    // Contractor confirms a bid the owner awarded to them. Selected -> Confirmed.
    [HttpPost("{bidId}/confirm")]
    public async Task<IActionResult> Confirm(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.ConfirmBidAsync(contractorId, bidId);
        return RespondWithList(result, "Bid confirmed successfully.");
    }

    // POST /api/bids/{bidId}/back-off
    // Legal from Selected, Confirmed, or InProgress -> Withdrawn.
    [HttpPost("{bidId}/back-off")]
    public async Task<IActionResult> BackOff(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.BackOffBidAsync(contractorId, bidId);
        return RespondWithList(result, "You've backed off this bid.");
    }

    // POST /api/bids/{bidId}/cancel
    // Pending -> Withdrawn.
    [HttpPost("{bidId}/cancel")]
    public async Task<IActionResult> Cancel(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.CancelBidAsync(contractorId, bidId);
        return RespondWithList(result, "Bid cancelled successfully.");
    }

    // POST /api/bids/{bidId}/mark-work-done
    // InProgress -> stamps the underlying project's SubmittedDate.
    [HttpPost("{bidId}/mark-work-done")]
    public async Task<IActionResult> MarkWorkDone(Guid bidId)
    {
        var contractorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _bidService.MarkWorkDoneAsync(contractorId, bidId);
        return RespondWithList(result, "Work marked as done.");
    }

    private IActionResult RespondWithList(List<MyBidItemDto>? result, string successMessage)
    {
        if (result == null)
        {
            return NotFound(new ApiResponse<List<MyBidItemDto>?>
            {
                Success = false,
                Message = "Bid not found.",
                Data = null
            });
        }

        return Ok(new ApiResponse<List<MyBidItemDto>>
        {
            Success = true,
            Message = successMessage,
            Data = result
        });
    }
}
