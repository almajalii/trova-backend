using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.RepostProject;
using TrovaBackend.Models;

namespace TrovaBackend.Services.RepostProject;

public class RepostProjectService : IRepostProjectService
{
    private static readonly string[] RepostableStatuses =
    {
        ProjectStatus.ContractorBackedOff, ProjectStatus.GuaranteeRejectedByYou
    };

    private readonly AppDbContext _db;

    public RepostProjectService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RepostDraftDto?> GetDraftAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null || !RepostableStatuses.Contains(project.Status))
            return null;

        var contractorName = await ResolveContractorNameAsync(project);

        return new RepostDraftDto
        {
            OriginalProjectId = project.ProjectCode,
            Reason = project.Status == ProjectStatus.ContractorBackedOff
                ? RepostReason.ContractorBackedOff
                : RepostReason.GuaranteeRejectedByOwner,
            ContractorName = contractorName,
            Title = project.Title,
            Sector = project.Sector,
            ContractValueJod = project.ContractValueJod,
            MinRequiredScore = project.MinimumRequiredScore,
            MinContractorClassification = project.MinimumClassification,
            Description = project.Description,

            Location = project.Location,
            Currency = project.Currency,
            TimelineText = project.TimelineText,
            Milestones = project.Milestones,
            GuaranteeTypeRequired = project.GuaranteeTypeRequired,
            PaymentTerms = project.PaymentTerms,
            BidSubmissionDeadline = project.BidSubmissionDeadline,
        };
    }

    public async Task<RepostProjectResponse> SubmitRepostAsync(Guid ownerId, string projectId, RepostProjectRequest request)
    {
        var original = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (original == null)
            throw new KeyNotFoundException("Project not found.");

        if (!RepostableStatuses.Contains(original.Status))
            throw new InvalidOperationException("This project can no longer be reposted.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Sector))
            throw new ArgumentException("Sector is required.");
        if (request.ContractValueJod < 0)
            throw new ArgumentException("Contract value cannot be negative.");
        if (request.MinRequiredScore < 0 || request.MinRequiredScore > 100)
            throw new ArgumentException("Minimum required score must be between 0 and 100.");
        if (request.BidSubmissionDeadline <= DateTime.UtcNow)
            throw new ArgumentException("Bid submission deadline must be in the future.");

        var newProject = new Project
        {
            OwnerId = ownerId,
            ProjectCode = await GenerateUniqueProjectCodeAsync(),

            Title = request.Title.Trim(),
            Sector = request.Sector.Trim(),
            Location = request.Location.Trim(),

            ContractValueJod = request.ContractValueJod,
            Currency = request.Currency.Trim().ToUpperInvariant(),

            TimelineText = request.TimelineText.Trim(),
            Milestones = request.Milestones.Trim(),

            GuaranteeTypeRequired = request.GuaranteeTypeRequired.Trim(),
            PaymentTerms = request.PaymentTerms.Trim(),
            Description = request.Description.Trim(),

            MinimumRequiredScore = request.MinRequiredScore,
            MinimumClassification = ValidateClassificationCode(request.MinContractorClassification),

            BidSubmissionDeadline = DateTime.SpecifyKind(request.BidSubmissionDeadline, DateTimeKind.Utc),
            Status = ProjectStatus.OpenForBids,
        };

        _db.Projects.Add(newProject);

        // Drops the original out of the active list (ActiveStatuses in
        // ProjectService doesn't include Cancelled) without deleting its
        // history — same "Class B or higher" style read-time labels apply
        // via GetStatusMeta once that's updated to know about Cancelled.
        original.Status = ProjectStatus.Cancelled;
        original.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new RepostProjectResponse { NewProjectId = newProject.ProjectCode };
    }

    private async Task<string> ResolveContractorNameAsync(Project project)
    {
        if (!project.AwardedBidId.HasValue) return "Unknown";

        var awardedBid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value);
        if (awardedBid == null) return "Unknown";

        var contractorCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == awardedBid.ContractorId);
        if (contractorCompany != null)
            return string.IsNullOrWhiteSpace(contractorCompany.TradingName) ? contractorCompany.LegalCompanyName : contractorCompany.TradingName;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == awardedBid.ContractorId);
        return user?.Name ?? "Unknown";
    }

    // Bare "A" | "B" | "C" only, same rule as PostProjectRequest's
    // [RegularExpression("^(A|B|C)$")] — the repost form is a fixed
    // A/B/C enum now, no free-text reconstruction needed.
    private static string ValidateClassificationCode(string raw)
    {
        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed is "A" or "B" or "C")
            return trimmed;

        throw new ArgumentException("Minimum classification must be 'A', 'B', or 'C'.");
    }

    private async Task<string> GenerateUniqueProjectCodeAsync()
    {
        var random = new Random();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"TRV-PRJ-{random.Next(10000, 99999)}";
            var exists = await _db.Projects.AnyAsync(p => p.ProjectCode == candidate);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Could not generate a unique project code. Please try again.");
    }
}