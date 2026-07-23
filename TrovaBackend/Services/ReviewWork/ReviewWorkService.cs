using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.ReviewWork;
using TrovaBackend.Models;

namespace TrovaBackend.Services.ReviewWork;

public class ReviewWorkService : IReviewWorkService
{
    private readonly AppDbContext _db;

    public ReviewWorkService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SubmittedWorkDto?> GetSubmittedWorkAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null || project.Status != ProjectStatus.PendingReview || project.SubmittedDate == null)
            return null;

        var awardedBid = project.AwardedBidId.HasValue
            ? await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value)
            : null;

        var contractorName = "Unknown";
        AwardedBidderDto? awardedBidder = null;
        if (awardedBid != null)
        {
            var contractorCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == awardedBid.ContractorId);
            contractorName = contractorCompany != null
                ? (string.IsNullOrWhiteSpace(contractorCompany.TradingName) ? contractorCompany.LegalCompanyName : contractorCompany.TradingName)
                : (await _db.Users.FirstOrDefaultAsync(u => u.Id == awardedBid.ContractorId))?.Name ?? "Unknown";

            awardedBidder = new AwardedBidderDto
            {
                BidId = awardedBid.Id.ToString(),
                CompanyName = contractorName,
                Classification = contractorCompany?.ClassificationCode ?? string.Empty,
                Eligible = true
            };
        }

        // Same "Active · TRV-GT-XXXXX" shape Project Detail's
        // GuaranteeRowText uses elsewhere — only ever populated once a
        // guarantee has actually been approved (project couldn't have
        // reached PendingReview otherwise).
        var guaranteeApp = await _db.GuaranteeApplications
            .Where(g => g.ProjectId == project.Id && g.Status == GuaranteeStatus.Approved)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        return new SubmittedWorkDto
        {
            ProjectId = project.ProjectCode,
            ProjectTitle = project.Title,
            Sector = project.Sector,
            Location = project.Location,
            ContractValueJod = project.ContractValueJod,
            TimelineText = project.TimelineText,
            Milestones = project.Milestones,
            GuaranteeTypeRequired = project.GuaranteeTypeRequired,
            PaymentTerms = project.PaymentTerms,
            ContractorName = contractorName,
            AwardedBidder = awardedBidder,
            SubmittedDate = project.SubmittedDate.Value.ToString("yyyy-MM-dd"),
            GuaranteeRowText = guaranteeApp != null ? $"Active · {guaranteeApp.ApplicationCode}" : null,
        };
    }

    public async Task ConfirmCompleteAsync(Guid ownerId, string projectId)
    {
        var project = await ResolvePendingReviewProjectAsync(ownerId, projectId);

        project.Status = ProjectStatus.Completed;
        project.UpdatedAt = DateTime.UtcNow;

        if (project.AwardedBidId.HasValue)
        {
            var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value);
            if (bid != null)
            {
                bid.Status = BidStatus.Completed;
                bid.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task FlagIssueAsync(Guid ownerId, string projectId)
    {
        var project = await ResolvePendingReviewProjectAsync(ownerId, projectId);

        // Bid.Status is deliberately left as InProgress — there's no
        // dedicated "disputed" bid state (same reasoning as
        // GuaranteeService.RejectAsync only flipping Project, not Bid).
        // Trova's back-office process is what resolves a dispute, not
        // anything modelled in this codebase yet.
        project.Status = ProjectStatus.Disputed;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    private async Task<Project> ResolvePendingReviewProjectAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found.");

        if (project.Status != ProjectStatus.PendingReview)
            throw new InvalidOperationException("This project isn't awaiting review.");

        return project;
    }
}
