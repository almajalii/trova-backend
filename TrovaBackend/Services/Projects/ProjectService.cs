using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Projects;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Projects;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;

    public ProjectService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PostProjectResponse> PostProjectAsync(Guid ownerId, PostProjectRequest request)
    {
        var project = new Project
        {
            OwnerId = ownerId,
            ProjectCode = await GenerateUniqueProjectCodeAsync(),

            Title = request.Title.Trim(),
            Sector = request.Sector.Trim(),
            Location = request.Location.Trim(),

            ContractValueJod = request.ContractValue,
            Currency = request.Currency.Trim().ToUpperInvariant(),

            TimelineText = request.Duration.Trim(),
            Milestones = request.Milestones.Trim(),

            GuaranteeTypeRequired = request.GuaranteeType.Trim(),
            PaymentTerms = request.PaymentTerms.Trim(),
            Description = request.Description.Trim(),

            MinimumRequiredScore = request.MinimumRequiredScore,
            MinimumClassification = request.MinimumClassification.Trim().ToUpperInvariant(),

            BidSubmissionDeadline = DateTime.SpecifyKind(request.BidSubmissionDeadline, DateTimeKind.Utc),
            Status = ProjectStatus.OpenForBids,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return new PostProjectResponse { ProjectId = project.ProjectCode };
    }

    // TRV-PRJ-XXXXX, checked against the table for collisions and retried —
    // same "random code, verify uniqueness" style as the 6-digit codes in
    // AuthService, just persisted permanently instead of expiring.
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