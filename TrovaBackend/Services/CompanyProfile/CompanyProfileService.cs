using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Services.Shared;

namespace TrovaBackend.Services.CompanyProfile;

public interface ICompanyProfileService
{
    // Always returns a value (never null) — an unreviewed contractor just
    // gets averageRating 0 / totalReviews 0 / items [], not a 404. There's
    // no "hasn't set this up yet" state to distinguish here the way there
    // is for Company Details, since this is purely aggregated read data.
    Task<BidderReviewsSummaryDto> GetReviewsAsync(Guid contractorId);
}

public class CompanyProfileService : ICompanyProfileService
{
    private readonly AppDbContext _db;

    public CompanyProfileService(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/company-profile/reviews. Reuses BidderReviewsSummaryDto /
    // BidderReviewItemDto from DTOs.Bids — same exact shape as the
    // "reviews" block already nested inside GET
    // /bids/{bidId}/company-profile, just returned as its own top-level
    // response here instead of embedded. Both pull from the same shared
    // helper so the numbers for a given contractor always agree.
    public async Task<BidderReviewsSummaryDto> GetReviewsAsync(Guid contractorId)
    {
        var (avgRating, items) = await ContractorTrackRecordHelper.GetReviewSummaryAsync(_db, contractorId);

        return new BidderReviewsSummaryDto
        {
            AverageRating = Math.Round(avgRating, 1),
            TotalReviews = items.Count,
            Items = items
        };
    }
}
