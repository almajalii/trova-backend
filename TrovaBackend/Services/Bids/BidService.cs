using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Bids;

public class BidService : IBidService
{
    private static readonly string[] ActiveStatuses =
    {
        BidStatus.Submitted, BidStatus.PendingConfirmation, BidStatus.Confirmed, BidStatus.InProgress
    };

    private static readonly string[] ClosedStatuses =
    {
        BidStatus.NotSelected, BidStatus.Completed, BidStatus.BackedOff
    };

    private readonly AppDbContext _db;

    public BidService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<MyBidItemDto>> GetMyBidsAsync(Guid contractorId)
    {
        var bids = await _db.Bids
            .Where(b => b.ContractorId == contractorId && ActiveStatuses.Contains(b.Status))
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();

        return await BuildMyBidsAsync(bids);
    }

    public async Task<List<BidHistoryItemDto>> GetHistoryAsync(Guid contractorId)
    {
        var bids = await _db.Bids
            .Where(b => b.ContractorId == contractorId && ClosedStatuses.Contains(b.Status))
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();

        if (bids.Count == 0) return new List<BidHistoryItemDto>();

        var projects = await _db.Projects
            .Where(p => bids.Select(b => b.ProjectId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var ownerNames = await GetCompanyNamesAsync(
            bids.Where(b => projects.ContainsKey(b.ProjectId)).Select(b => projects[b.ProjectId].OwnerId));

        return bids.Select(b =>
        {
            projects.TryGetValue(b.ProjectId, out var project);
            ownerNames.TryGetValue(project?.OwnerId ?? Guid.Empty, out var companyName);

            return new BidHistoryItemDto
            {
                BidId = b.Id.ToString(),
                ProjectId = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                CompanyName = companyName ?? "Unknown",
                BidAmountJod = b.BidAmountJod,
                Status = BidStatusMapper.ToExternal(b.Status),
                Note = b.Status == BidStatus.Completed ? null : b.Note,
                // No Review entity yet — see BidDTOs.cs.
                Review = null
            };
        }).ToList();
    }

    // Contractor confirms a bid the owner awarded to them. Only flips
    // Bid.Status — Project.Status stays Awarded.
    public async Task<List<MyBidItemDto>?> ConfirmBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.PendingConfirmation)
            throw new InvalidOperationException("This bid isn't awaiting confirmation.");

        bid.Status = BidStatus.Confirmed;
        bid.Note = null;
        bid.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetMyBidsAsync(contractorId);
    }

    // Legal from Selected, Confirmed, or InProgress. Project.AwardedBidId
    // is deliberately left pointing at this bid rather than cleared — the
    // read-side subtitle/detailText logic ("{contractorName} backed off")
    // depends on being able to resolve the bid to get that name. Other
    // bids that were marked NotSelected at Award time are NOT reopened —
    // judgment call carried over from the original Confirm/Back Off pass.
    public async Task<List<MyBidItemDto>?> BackOffBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        var backoffableStatuses = new[] { BidStatus.PendingConfirmation, BidStatus.Confirmed, BidStatus.InProgress };
        if (!backoffableStatuses.Contains(bid.Status))
            throw new InvalidOperationException("This bid can no longer be backed off.");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        // "After a guarantee rejection" is signalled by the project
        // already sitting in GuaranteeRejectedByYou when the contractor
        // backs off — that status exists in the schema but nothing sets
        // it yet (guarantee flow isn't built in this pass).
        bid.Note = project.Status == ProjectStatus.GuaranteeRejectedByYou
            ? "You withdrew after the guarantee was rejected"
            : "You backed off";
        bid.Status = BidStatus.BackedOff;
        bid.UpdatedAt = DateTime.UtcNow;

        project.Status = ProjectStatus.ContractorBackedOff;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetMyBidsAsync(contractorId);
    }

    public async Task<List<MyBidItemDto>?> CancelBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.Submitted)
            throw new InvalidOperationException("This bid can't be cancelled.");

        bid.Status = BidStatus.BackedOff;
        bid.Note = "Bid cancelled";
        bid.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetMyBidsAsync(contractorId);
    }

    // Doesn't change Bid.Status — stays InProgress. Just stamps the
    // project's SubmittedDate, which GET /projects/{id}/submitted-work
    // (not built in this pass) will read off.
    public async Task<List<MyBidItemDto>?> MarkWorkDoneAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.InProgress)
            throw new InvalidOperationException("This bid isn't in progress.");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        project.SubmittedDate = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetMyBidsAsync(contractorId);
    }

    private async Task<List<MyBidItemDto>> BuildMyBidsAsync(List<Bid> bids)
    {
        if (bids.Count == 0) return new List<MyBidItemDto>();

        var projects = await _db.Projects
            .Where(p => bids.Select(b => b.ProjectId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var ownerNames = await GetCompanyNamesAsync(
            bids.Where(b => projects.ContainsKey(b.ProjectId)).Select(b => projects[b.ProjectId].OwnerId));

        return bids.Select(b =>
        {
            projects.TryGetValue(b.ProjectId, out var project);
            ownerNames.TryGetValue(project?.OwnerId ?? Guid.Empty, out var companyName);

            return new MyBidItemDto
            {
                BidId = b.Id.ToString(),
                ProjectId = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                CompanyName = companyName ?? "Unknown",
                BidAmountJod = b.BidAmountJod,
                Status = BidStatusMapper.ToExternal(b.Status),
                Note = b.Note ?? DefaultActiveNote(b.Status),
                GuaranteeExpiresInDays = null // see BidDTOs.cs
            };
        }).ToList();
    }

    // Display text for the active states, when nothing's been explicitly
    // stored on the bid (Note is only ever persisted for terminal states).
    private static string? DefaultActiveNote(string status) => status switch
    {
        BidStatus.Submitted => "Waiting for owner's decision",
        BidStatus.PendingConfirmation => "Awarded to you — confirm to proceed",
        BidStatus.Confirmed => "Waiting for guarantee approval",
        BidStatus.InProgress => "Work in progress",
        _ => null
    };

    // Same pattern as GetContractorNamesAsync in ProjectService — generic
    // by userId, works for resolving project owners' company names here.
    private async Task<Dictionary<Guid, string>> GetCompanyNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var names = await _db.CompanyDetails
            .Where(c => ids.Contains(c.UserId))
            .ToDictionaryAsync(
                c => c.UserId,
                c => string.IsNullOrWhiteSpace(c.TradingName) ? c.LegalCompanyName : c.TradingName);

        var missingIds = ids.Where(id => !names.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            var userNames = await _db.Users
                .Where(u => missingIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            foreach (var kv in userNames)
                names[kv.Key] = kv.Value;
        }

        return names;
    }
}

// Maps internal snake_case Bid.Status values onto the external My Bids
// API vocabulary. Kept separate from ProjectService's simple
// .ToUpperInvariant() convention because the mapping here isn't 1:1
// (PendingConfirmation -> SELECTED, BackedOff -> WITHDRAWN, etc.).
public static class BidStatusMapper
{
    public static string ToExternal(string internalStatus) => internalStatus switch
    {
        BidStatus.Submitted => "PENDING",
        BidStatus.PendingConfirmation => "SELECTED",
        BidStatus.Confirmed => "CONFIRMED",
        BidStatus.InProgress => "IN_PROGRESS",
        BidStatus.NotSelected => "REJECTED",
        BidStatus.BackedOff => "WITHDRAWN",
        BidStatus.Completed => "COMPLETED",
        _ => internalStatus.ToUpperInvariant()
    };
}
