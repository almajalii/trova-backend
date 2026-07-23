using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs;
using TrovaBackend.DTOs.Admin;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Admin;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public AdminService(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    // ── Whitelist (pending users) ───────────────────────────────────────

    public async Task<List<AdminPendingUserDto>> GetPendingUsersAsync()
    {
        var users = await _db.Users
            .Where(u => u.ApprovalStatus == UserApprovalStatus.Pending)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        if (users.Count == 0)
            return new List<AdminPendingUserDto>();

        var userIds = users.Select(u => u.Id).ToList();
        var companyDetailsByUserId = await _db.CompanyDetails
            .Where(c => userIds.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId);

        return users.Select(u => new AdminPendingUserDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Phone = u.Phone,
            RequestedDate = u.CreatedAt.ToString("yyyy-MM-dd"),
            CompanyDetails = companyDetailsByUserId.TryGetValue(u.Id, out var c)
                ? MapCompanyDetails(c)
                : null,
        }).ToList();
    }

    public async Task ApproveUserAsync(Guid userId)
    {
        var user = await ResolvePendingUserAsync(userId);

        user.ApprovalStatus = UserApprovalStatus.Approved;
        user.ApprovedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendAccountApprovedEmailAsync(user.Email, user.Name);
        }
        catch (Exception ex)
        {
            // Same "don't block the state change on email delivery"
            // convention as the rest of AuthService.
            Console.WriteLine($"[EMAIL FAILED] Approval email for {user.Email} — {ex.Message}");
        }
    }

    public async Task RejectUserAsync(Guid userId, string reason)
    {
        var user = await ResolvePendingUserAsync(userId);

        user.ApprovalStatus = UserApprovalStatus.Rejected;
        user.RejectionReason = reason;
        user.ApprovedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendAccountRejectedEmailAsync(user.Email, user.Name, reason);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL FAILED] Rejection email for {user.Email} — {ex.Message}");
        }
    }

    private async Task<User> ResolvePendingUserAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.ApprovalStatus != UserApprovalStatus.Pending)
            throw new InvalidOperationException("This user has already been reviewed.");

        return user;
    }

    // ── Users ────────────────────────────────────────────────────────────

    public async Task<List<AdminUserSummaryDto>> GetUsersAsync()
    {
        var users = await _db.Users
            .Where(u => u.Role == "user")
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        if (users.Count == 0)
            return new List<AdminUserSummaryDto>();

        var userIds = users.Select(u => u.Id).ToList();
        var companyDetailsByUserId = await _db.CompanyDetails
            .Where(c => userIds.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId);

        return users.Select(u =>
        {
            companyDetailsByUserId.TryGetValue(u.Id, out var c);
            var company = c == null
                ? null
                : (string.IsNullOrWhiteSpace(c.TradingName) ? c.LegalCompanyName : c.TradingName);

            return new AdminUserSummaryDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Company = company,
                ApprovalStatus = u.ApprovalStatus,
                JoinedDate = u.CreatedAt.ToString("yyyy-MM-dd"),
            };
        }).ToList();
    }

    // ── Disputes ─────────────────────────────────────────────────────────

    public async Task<List<AdminDisputeSummaryDto>> GetDisputesAsync()
    {
        var projects = await _db.Projects
            .Where(p => p.DisputeReason != null)
            .OrderByDescending(p => p.DisputeRaisedAt)
            .ToListAsync();

        if (projects.Count == 0)
            return new List<AdminDisputeSummaryDto>();

        var (contractorNames, ownerNames) = await ResolvePartyNamesAsync(projects);

        return projects.Select(p => new AdminDisputeSummaryDto
        {
            ProjectId = p.ProjectCode,
            ProjectTitle = p.Title,
            ContractorName = contractorNames.GetValueOrDefault(p.Id, "Unknown"),
            OwnerName = ownerNames.GetValueOrDefault(p.OwnerId, "Unknown"),
            Status = p.Status == ProjectStatus.Disputed ? "Open" : "Resolved",
            RaisedDate = (p.DisputeRaisedAt ?? p.UpdatedAt).ToString("yyyy-MM-dd"),
        }).ToList();
    }

    public async Task<AdminDisputeDetailDto?> GetDisputeAsync(string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectId && p.DisputeReason != null);
        if (project == null)
            return null;

        var (contractorNames, ownerNames) = await ResolvePartyNamesAsync(new List<Project> { project });

        return new AdminDisputeDetailDto
        {
            ProjectId = project.ProjectCode,
            ProjectTitle = project.Title,
            Sector = project.Sector,
            Location = project.Location,
            ContractValueJod = project.ContractValueJod,
            TimelineText = project.TimelineText,
            Milestones = project.Milestones,
            ContractorName = contractorNames.GetValueOrDefault(project.Id, "Unknown"),
            OwnerName = ownerNames.GetValueOrDefault(project.OwnerId, "Unknown"),
            Status = project.Status == ProjectStatus.Disputed ? "Open" : "Resolved",
            RaisedDate = (project.DisputeRaisedAt ?? project.UpdatedAt).ToString("yyyy-MM-dd"),
            DisputeReason = project.DisputeReason ?? string.Empty,
            SubmittedDate = project.SubmittedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ResolutionMessage = project.DisputeResolutionMessage,
            ResolvedDate = project.DisputeResolvedAt?.ToString("yyyy-MM-dd"),
        };
    }

    public async Task ResolveDisputeAsync(string projectId, string message)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectId && p.DisputeReason != null)
            ?? throw new KeyNotFoundException("Dispute not found.");

        if (project.Status != ProjectStatus.Disputed)
            throw new InvalidOperationException("This dispute has already been resolved.");

        // Always resolves to Completed — no separate "contractor was at
        // fault" outcome in this pass (confirmed decision: single message,
        // no outcome picker in the admin UI).
        project.Status = ProjectStatus.Completed;
        project.DisputeResolutionMessage = message;
        project.DisputeResolvedAt = DateTime.UtcNow;
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

        await NotifyPartiesOfResolutionAsync(project, message);
    }

    // ── Shared helpers ───────────────────────────────────────────────────

    private async Task<(Dictionary<Guid, string> contractorNamesByProjectId, Dictionary<Guid, string> ownerNamesByUserId)>
        ResolvePartyNamesAsync(List<Project> projects)
    {
        var ownerIds = projects.Select(p => p.OwnerId).Distinct().ToList();

        var contractorIdByProjectId = new Dictionary<Guid, Guid>();
        foreach (var p in projects)
        {
            if (p.AwardedBidId.HasValue)
            {
                var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == p.AwardedBidId.Value);
                if (bid != null)
                    contractorIdByProjectId[p.Id] = bid.ContractorId;
            }
        }

        var allUserIds = ownerIds.Concat(contractorIdByProjectId.Values).Distinct().ToList();

        var usersById = await _db.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var companyByUserId = await _db.CompanyDetails
            .Where(c => allUserIds.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId);

        string DisplayName(Guid userId)
        {
            if (companyByUserId.TryGetValue(userId, out var c))
                return string.IsNullOrWhiteSpace(c.TradingName) ? c.LegalCompanyName : c.TradingName;
            return usersById.GetValueOrDefault(userId, "Unknown");
        }

        var contractorNames = contractorIdByProjectId.ToDictionary(kv => kv.Key, kv => DisplayName(kv.Value));
        var ownerNames = ownerIds.ToDictionary(id => id, DisplayName);

        return (contractorNames, ownerNames);
    }

    private async Task NotifyPartiesOfResolutionAsync(Project project, string message)
    {
        var ownerTask = _db.Users.FirstOrDefaultAsync(u => u.Id == project.OwnerId);
        Guid? contractorId = project.AwardedBidId.HasValue
            ? (await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value))?.ContractorId
            : null;

        var owner = await ownerTask;
        var contractor = contractorId.HasValue
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == contractorId.Value)
            : null;

        foreach (var recipient in new[] { owner, contractor })
        {
            if (recipient == null) continue;
            try
            {
                await _emailService.SendDisputeResolvedEmailAsync(recipient.Email, recipient.Name, project.Title, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL FAILED] Dispute resolution email for {recipient.Email} — {ex.Message}");
            }
        }
    }

    private static CompanyDetailsRecordDto MapCompanyDetails(TrovaBackend.Models.CompanyDetails c) => new()
    {
        LegalCompanyName = c.LegalCompanyName,
        TradingName = c.TradingName,
        RegistrationNumber = c.RegistrationNumber,
        TaxVatNumber = c.TaxVatNumber,
        LegalStructure = c.LegalStructure,
        YearOfEstablishment = c.YearOfEstablishment,
        RegisteredAddress = c.RegisteredAddress,
        CountryOfRegistration = c.CountryOfRegistration,
        PrimaryContactName = c.PrimaryContactName,
        PositionTitle = c.PositionTitle,
        PrimaryEmail = c.PrimaryEmail,
        PrimaryPhoneNumber = c.PrimaryPhoneNumber,
        BusinessLicenseNumber = c.BusinessLicenseNumber,
        ContractorClassificationGrade = c.ContractorClassificationGrade,
        Sectors = c.Sectors,
        YearsOfExperience = c.YearsOfExperience,
        TeamSize = c.TeamSize,
        AnnualRevenueJod = c.AnnualRevenueJod,
        PrimaryBankName = c.PrimaryBankName,
        IbanNumber = c.IbanNumber,
        SwiftBicCode = c.SwiftBicCode,
        BankBranchNameCity = c.BankBranchNameCity,
        Classification = new ScoreClassificationDto
        {
            Code = c.ClassificationCode,
            Label = c.ClassificationLabel,
        },
    };
}
