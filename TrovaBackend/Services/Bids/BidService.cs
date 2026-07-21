using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Bids;

public class BidService : IBidService
{
    private readonly AppDbContext _db;

    public BidService(AppDbContext db)
    {
        _db = db;
    }

    // Contractor confirms a bid the owner awarded to them. Only flips
    // Bid.Status — Project.Status stays Awarded. The read side (My Projects
    // / Project Detail) already branches on Bid.Status == Confirmed to show
    // "waiting for guarantee" text and light up the Guarantee Active
    // timeline step, so nothing else needs to change here.
    public async Task<BidActionResponse?> ConfirmBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.PendingConfirmation)
            throw new InvalidOperationException("This bid isn't awaiting confirmation.");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        bid.Status = BidStatus.Confirmed;
        bid.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new BidActionResponse
        {
            BidId = bid.Id.ToString(),
            ProjectId = project.ProjectCode,
            Status = bid.Status.ToUpperInvariant(),
            ProjectStatus = project.Status.ToUpperInvariant()
        };
    }

    // Contractor declines an awarded bid. Bid.Status -> BackedOff and
    // Project.Status -> ContractorBackedOff (matches the confirmed business
    // rule: "if they dont confirm it should say contractor backed off").
    //
    // Project.AwardedBidId is deliberately left pointing at this bid rather
    // than cleared — the read-side subtitle/detailText logic
    // ("{contractorName} backed off") depends on being able to resolve the
    // bid to get that name. Other bids that were marked NotSelected at
    // Award time are NOT reopened; per the existing actionLabel design
    // ("Post Project Again" for ContractorBackedOff), the owner reposts
    // fresh rather than falling back to the next-ranked bidder. Flagging
    // this as a judgment call, not a confirmed requirement — easy to change
    // if you'd rather fall back to bidder #2 automatically.
    public async Task<BidActionResponse?> BackOffBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.PendingConfirmation)
            throw new InvalidOperationException("This bid isn't awaiting confirmation.");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        bid.Status = BidStatus.BackedOff;
        bid.UpdatedAt = DateTime.UtcNow;

        project.Status = ProjectStatus.ContractorBackedOff;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new BidActionResponse
        {
            BidId = bid.Id.ToString(),
            ProjectId = project.ProjectCode,
            Status = bid.Status.ToUpperInvariant(),
            ProjectStatus = project.Status.ToUpperInvariant()
        };
    }
}
