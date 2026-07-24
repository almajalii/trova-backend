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

    // GET /api/bids/{bidId}/company-profile. Owner-scoped: the caller must
    // own the project this bid belongs to — null (-> 404) otherwise, same
    // ownership pattern as everywhere else in this codebase. Every field
    // on the returned DTO is always fully populated; see the doc comment
    // on BidderCompanyProfileDto for why that matters here specifically.
    Task<BidderCompanyProfileDto?> GetCompanyProfileAsync(Guid ownerId, Guid bidId);

    // GET /api/bids/{bidId}/owner-profile. Reverse of GetCompanyProfileAsync:
    // contractor-scoped — the caller must be the contractor who placed this
    // bid — null (-> 404) otherwise, same ownership pattern as everywhere
    // else in this file.
    Task<OwnerProfileDto?> GetOwnerProfileAsync(Guid contractorId, Guid bidId);
}