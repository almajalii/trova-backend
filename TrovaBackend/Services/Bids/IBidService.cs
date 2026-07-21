using TrovaBackend.DTOs.Bids;

namespace TrovaBackend.Services.Bids;

public interface IBidService
{
    // Both return null if the bid doesn't exist or doesn't belong to this
    // contractor — same "exists but isn't yours 404s like it doesn't exist"
    // pattern used for Project ownership scoping elsewhere.
    Task<BidActionResponse?> ConfirmBidAsync(Guid contractorId, Guid bidId);
    Task<BidActionResponse?> BackOffBidAsync(Guid contractorId, Guid bidId);
}
