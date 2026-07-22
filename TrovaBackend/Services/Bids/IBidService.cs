using TrovaBackend.DTOs.Bids;

namespace TrovaBackend.Services.Bids;

public interface IBidService
{
    Task<List<MyBidItemDto>> GetMyBidsAsync(Guid contractorId);
    Task<List<BidHistoryItemDto>> GetHistoryAsync(Guid contractorId);

    // Null if the bid doesn't exist or isn't the caller's — same
    // ownership-scoped 404 pattern as the actions below and as
    // ProjectService's owner-scoped Detail().
    Task<BidDetailDto?> GetBidDetailAsync(Guid contractorId, Guid bidId);

    // All four actions return null if the bid doesn't exist or doesn't
    // belong to this contractor (same "exists but isn't yours 404s like
    // it doesn't exist" pattern used for Project ownership scoping
    // elsewhere), otherwise the caller's full updated active list.
    Task<List<MyBidItemDto>?> ConfirmBidAsync(Guid contractorId, Guid bidId);
    Task<List<MyBidItemDto>?> BackOffBidAsync(Guid contractorId, Guid bidId);
    Task<List<MyBidItemDto>?> CancelBidAsync(Guid contractorId, Guid bidId);
    Task<List<MyBidItemDto>?> MarkWorkDoneAsync(Guid contractorId, Guid bidId);
}
