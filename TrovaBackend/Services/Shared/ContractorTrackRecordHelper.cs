using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Shared;

// Real project/review history for a contractor, computed directly from
// Projects/Bids/Reviews. Pulled out into one place because as of this
// pass three different endpoints need the exact same numbers to agree
// with each other:
//   - GET /capability-score/me            (trackRecordStats)
//   - GET /bids/{bidId}/company-profile   (trackRecordStats + reviews)
//   - GET /company-profile/reviews        (reviews)
// Before this, CapabilityScore's TotalProjects/FailedProjects/AvgRating
// were hardcoded to 0 (see the comment that used to sit in
// CapabilityScoreService.RecalculateAsync) — computing them here once,
// shared, means the three endpoints can't quietly drift apart the way
// three separate inline implementations eventually would.
public static class ContractorTrackRecordHelper
{
    // Awarded/failed/currently-active counts, based on Project rows this
    // contractor's bid actually won (Project.AwardedBidId points at one
    // of their Bids) — not on Bid rows alone, since a contractor can have
    // many bids that were never awarded.
    public static async Task<(int TotalProjects, int FailedProjects, int CurrentProjects)> GetProjectStatsAsync(
        AppDbContext db, Guid contractorId)
    {
        var awardedProjects = await db.Projects
            .Where(p => p.AwardedBidId.HasValue)
            .Join(db.Bids.Where(b => b.ContractorId == contractorId),
                p => p.AwardedBidId!.Value, b => b.Id, (p, b) => p)
            .ToListAsync();

        var totalProjects = awardedProjects.Count;
        var failedProjects = awardedProjects.Count(p => p.Status == ProjectStatus.Failed);

        // "Currently active" includes PendingReview, not just
        // Awarded/InProgress — a project awaiting the owner's review is
        // still live work for this contractor, not yet resolved either way.
        var activeStatuses = new[] { ProjectStatus.Awarded, ProjectStatus.InProgress, ProjectStatus.PendingReview };
        var currentProjects = awardedProjects.Count(p => activeStatuses.Contains(p.Status));

        return (totalProjects, failedProjects, currentProjects);
    }

    // Every review this contractor has received (as Review.RevieweeId),
    // with reviewer/project names resolved and each review's 6 rating
    // categories collapsed into one rounded star value. AvgRating is the
    // average of those already-rounded per-review stars — the same
    // number `Items` would average out to — so a caller exposing both a
    // summary rating and a list of items never has them silently disagree.
    public static async Task<(double AvgRating, List<BidderReviewItemDto> Items)> GetReviewSummaryAsync(
        AppDbContext db, Guid contractorId)
    {
        var reviews = await db.Reviews.Where(r => r.RevieweeId == contractorId).ToListAsync();
        if (reviews.Count == 0) return (0.0, new List<BidderReviewItemDto>());

        var projectIds = reviews.Select(r => r.ProjectId).Distinct().ToList();
        var projectTitles = await db.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title);

        var reviewerNames = await ResolveCompanyNamesAsync(db, reviews.Select(r => r.ReviewerId));

        var items = reviews.Select(r => new BidderReviewItemDto
        {
            ReviewerName = reviewerNames.TryGetValue(r.ReviewerId, out var name) ? name : "Unknown",
            ProjectTitle = projectTitles.TryGetValue(r.ProjectId, out var title) ? title : string.Empty,
            Stars = ComputeStars(r),
            Comment = r.Comment
        }).ToList();

        return (items.Average(i => i.Stars), items);
    }

    // Same rounding rule as BidService.ComputeStars / BidReviewDto.Stars —
    // average of the six 1-5 categories (WouldYouRehire included, rated
    // 1-5 like the rest), rounded to the nearest whole star.
    public static int ComputeStars(Review review)
    {
        var average = (review.QualityOfWorkmanship + review.AdherenceToTimeline + review.AdherenceToBudgetScope +
                        review.CommunicationResponsiveness + review.SiteSafetyCompliance + review.WouldYouRehire) / 6.0;
        return (int)Math.Round(average, MidpointRounding.AwayFromZero);
    }

    // Same "trading name else legal name else user name" pattern used
    // throughout the codebase (ProjectService.GetContractorNamesAsync,
    // BidService.GetCompanyNamesAsync, etc.) — duplicated here rather
    // than shared further since each caller has it as a private instance
    // method tied to its own DbContext injection; this is the one
    // static, DbContext-parameterized copy other static helpers can call.
    private static async Task<Dictionary<Guid, string>> ResolveCompanyNamesAsync(AppDbContext db, IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var names = await db.CompanyDetails
            .Where(c => ids.Contains(c.UserId))
            .ToDictionaryAsync(
                c => c.UserId,
                c => string.IsNullOrWhiteSpace(c.TradingName) ? c.LegalCompanyName : c.TradingName);

        var missingIds = ids.Where(id => !names.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            var userNames = await db.Users
                .Where(u => missingIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            foreach (var kv in userNames)
                names[kv.Key] = kv.Value;
        }

        return names;
    }
}
