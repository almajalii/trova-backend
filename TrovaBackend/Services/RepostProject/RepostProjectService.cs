using System.Text.RegularExpressions;
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
            MinContractorClassification = ClassificationDisplayText(project.MinimumClassification),
            Description = project.Description,
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

        // Fields the repost form doesn't expose (location, timeline,
        // milestones, guarantee type, payment terms) carry over unchanged
        // from the original project — only the fields RepostProjectLayout
        // actually renders are editable here. Bid submission deadline
        // isn't collected either; defaulting to 14 days out, same as a
        // reasonable fresh posting window.
        var newProject = new Project
        {
            OwnerId = ownerId,
            ProjectCode = await GenerateUniqueProjectCodeAsync(),

            Title = request.Title.Trim(),
            Sector = request.Sector.Trim(),
            Location = original.Location,

            ContractValueJod = request.ContractValueJod,
            Currency = original.Currency,

            TimelineText = original.TimelineText,
            Milestones = original.Milestones,

            GuaranteeTypeRequired = original.GuaranteeTypeRequired,
            PaymentTerms = original.PaymentTerms,
            Description = request.Description.Trim(),

            MinimumRequiredScore = request.MinRequiredScore,
            MinimumClassification = ExtractClassificationCode(request.MinContractorClassification),

            BidSubmissionDeadline = DateTime.UtcNow.AddDays(14),
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

    // "A" -> "Class A or higher" — best-effort reconstruction of the
    // free-text field the repost form actually edits, seeded from the
    // bare code Post Project itself stores. Not stored anywhere; only
    // ever shown as the draft's starting text.
    private static string ClassificationDisplayText(string code) => code.Trim().ToUpperInvariant() switch
    {
        "A" => "Class A or higher (Large)",
        "B" => "Class B or higher (Medium+)",
        "C" => "Class C or higher (Small+)",
        _ => code,
    };

    // Reverse direction: pulls a bare A/B/C back out of whatever the
    // owner left in the free-text classification field, since
    // Project.MinimumClassification stores just the code. Accepts either
    // a bare letter or a "Class X ..." phrase.
    private static string ExtractClassificationCode(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 1 && "ABCabc".Contains(trimmed))
            return trimmed.ToUpperInvariant();

        var match = Regex.Match(trimmed, @"Class\s+(A|B|C)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.ToUpperInvariant();

        throw new ArgumentException("minContractorClassification must be 'A', 'B', 'C', or a phrase like 'Class B or higher'.");
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
