using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Shared;

// Builds the owner-facing profile DTO directly from an ownerId. Pulled out
// of BidService so the two lookup paths — bid-scoped (GET
// /bids/{bidId}/owner-profile, post-bid) and project-scoped (GET
// /projects/{projectId}/owner-profile, pre-bid, browse screen) — can't
// silently drift apart, same reasoning as ContractorTrackRecordHelper on
// the contractor side. Callers are responsible for their own visibility
// scoping (bid ownership vs. project openness) before calling this; this
// helper only builds the DTO for whatever ownerId it's given.
public static class OwnerProfileHelper
{
    public static async Task<OwnerProfileDto> BuildAsync(AppDbContext db, Guid ownerId)
    {
        var company = await db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == ownerId);
        var ownerUser = company == null
            ? await db.Users.FirstOrDefaultAsync(u => u.Id == ownerId)
            : null;
        var tradingName = company != null
            ? (string.IsNullOrWhiteSpace(company.TradingName) ? company.LegalCompanyName : company.TradingName)
            : (ownerUser?.Name ?? "Unknown Owner");

        // Every project this owner has ever posted — drives both
        // SectorsPosted and TrackRecordStats below, same "compute once,
        // slice twice" approach as BidService.BuildMyBidsAsync.
        var ownerProjects = await db.Projects.Where(p => p.OwnerId == ownerId).ToListAsync();

        var sectorsPosted = ownerProjects.Select(p => p.Sector).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        var totalProjectsPosted = ownerProjects.Count;
        var completedProjects = ownerProjects.Count(p => p.Status == ProjectStatus.Completed);
        // "Active" = still live in some form — everything except a
        // completed, cancelled, or failed terminal state. Mirrors the
        // spirit of ContractorTrackRecordHelper's activeStatuses set, just
        // from the owner's side of the same Project rows.
        var activeProjects = ownerProjects.Count(p =>
            p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Failed);

        return new OwnerProfileDto
        {
            TradingName = tradingName,
            RegistrationNumber = company?.RegistrationNumber ?? string.Empty,
            TaxVatNumber = company?.TaxVatNumber ?? string.Empty,
            LegalStructure = company?.LegalStructure ?? string.Empty,
            YearOfEstablishment = company?.YearOfEstablishment ?? 0,
            RegisteredAddress = company?.RegisteredAddress ?? string.Empty,
            CountryOfRegistration = company?.CountryOfRegistration ?? string.Empty,
            PrimaryContactName = company?.PrimaryContactName ?? string.Empty,
            PositionTitle = company?.PositionTitle ?? string.Empty,
            PrimaryEmail = company?.PrimaryEmail ?? ownerUser?.Email ?? string.Empty,
            PrimaryPhoneNumber = company?.PrimaryPhoneNumber ?? ownerUser?.Phone ?? string.Empty,
            BusinessLicenseNumber = company?.BusinessLicenseNumber ?? string.Empty,
            YearsOfExperience = company != null && company.YearOfEstablishment > 0
                ? Math.Max(0, DateTime.UtcNow.Year - company.YearOfEstablishment)
                : 0,
            SectorsPosted = sectorsPosted,

            TrackRecordStats = new OwnerTrackRecordStatsDto
            {
                TotalProjectsPosted = totalProjectsPosted,
                ActiveProjects = activeProjects,
                CompletedProjects = completedProjects,
                AvgRating = 0.0 // see the doc comment on OwnerTrackRecordStatsDto.AvgRating
            }
        };
    }
}