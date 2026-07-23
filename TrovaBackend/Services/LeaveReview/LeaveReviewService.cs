using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.LeaveReview;
using TrovaBackend.Models;

namespace TrovaBackend.Services.LeaveReview;

public class LeaveReviewService : ILeaveReviewService
{
    private readonly AppDbContext _db;

    public LeaveReviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReviewContextDto?> GetContextAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null || project.Status != ProjectStatus.Completed)
            return null;

        var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.ProjectId == project.Id);
        if (alreadyReviewed) return null;

        var (contractorName, awardedBidder) = await ResolveContractorContextAsync(project);

        return new ReviewContextDto
        {
            ProjectId = project.ProjectCode,
            ContractorName = contractorName,
            AwardedBidder = awardedBidder,
            ProjectTitle = project.Title,
            // No ProjectStatusHistory table exists yet (same gap noted in
            // ProjectService) — UpdatedAt is the closest proxy for "when
            // this reached Completed", same convention used there.
            CompletedDate = project.UpdatedAt.ToString("yyyy-MM-dd"),
            Ratings = new Dictionary<string, int>(),
        };
    }

    public async Task SubmitReviewAsync(Guid ownerId, string projectId, SubmitReviewRequest request)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found.");

        if (project.Status != ProjectStatus.Completed)
            throw new InvalidOperationException("This project isn't completed yet.");

        if (await _db.Reviews.AnyAsync(r => r.ProjectId == project.Id))
            throw new InvalidOperationException("You've already reviewed this project.");

        if (!project.AwardedBidId.HasValue)
            throw new InvalidOperationException("No awarded contractor found for this project.");

        var awardedBid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value);
        if (awardedBid == null)
            throw new InvalidOperationException("No awarded contractor found for this project.");

        var ratings = ValidateAndExtractRatings(request.Ratings);

        var review = new Review
        {
            ProjectId = project.Id,
            ReviewerId = ownerId,
            RevieweeId = awardedBid.ContractorId,

            QualityOfWorkmanship = ratings[ReviewCategoryKeys.QualityOfWorkmanship],
            AdherenceToTimeline = ratings[ReviewCategoryKeys.AdherenceToTimeline],
            AdherenceToBudgetScope = ratings[ReviewCategoryKeys.AdherenceToBudgetScope],
            CommunicationResponsiveness = ratings[ReviewCategoryKeys.CommunicationResponsiveness],
            SiteSafetyCompliance = ratings[ReviewCategoryKeys.SiteSafetyCompliance],
            WouldYouRehire = ratings[ReviewCategoryKeys.WouldYouRehire],

            Comment = request.Comment?.Trim() ?? string.Empty,
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
    }

    // Mirrors LeaveReviewDraft.isComplete on the frontend: every one of
    // the six categories must be present and rated 1-5. Enforced here too
    // since the frontend's client-side check is easy to route around.
    private static Dictionary<string, int> ValidateAndExtractRatings(Dictionary<string, int> ratings)
    {
        foreach (var key in ReviewCategoryKeys.All)
        {
            if (!ratings.TryGetValue(key, out var value) || value < 1 || value > 5)
                throw new ArgumentException($"Rating for '{key}' must be between 1 and 5.");
        }

        return ratings;
    }

    private async Task<(string ContractorName, AwardedBidderDto? AwardedBidder)> ResolveContractorContextAsync(Project project)
    {
        if (!project.AwardedBidId.HasValue) return ("Unknown", null);

        var awardedBid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value);
        if (awardedBid == null) return ("Unknown", null);

        var contractorCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == awardedBid.ContractorId);
        string contractorName;
        if (contractorCompany != null)
        {
            contractorName = string.IsNullOrWhiteSpace(contractorCompany.TradingName)
                ? contractorCompany.LegalCompanyName
                : contractorCompany.TradingName;
        }
        else
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == awardedBid.ContractorId);
            contractorName = user?.Name ?? "Unknown";
        }

        var awardedBidder = new AwardedBidderDto
        {
            BidId = awardedBid.Id.ToString(),
            CompanyName = contractorName,
            Classification = contractorCompany?.ClassificationCode ?? string.Empty,
            Eligible = true
        };

        return (contractorName, awardedBidder);
    }
}
