using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Bids;
using TrovaBackend.Models;
using TrovaBackend.Services.CapabilityScore;
using TrovaBackend.Services.Shared;

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
    private readonly ICapabilityScoreService _capabilityScoreService;

    public BidService(AppDbContext db, ICapabilityScoreService capabilityScoreService)
    {
        _db = db;
        _capabilityScoreService = capabilityScoreService;
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

        // Only Completed bids can have one — a review requires the
        // project to have actually reached Completed (see
        // LeaveReviewService.SubmitReviewAsync).
        var completedProjectIds = bids
            .Where(b => b.Status == BidStatus.Completed && projects.ContainsKey(b.ProjectId))
            .Select(b => projects[b.ProjectId].Id)
            .ToList();
        var reviews = completedProjectIds.Count == 0
            ? new Dictionary<Guid, Review>()
            : await _db.Reviews
                .Where(r => completedProjectIds.Contains(r.ProjectId))
                .ToDictionaryAsync(r => r.ProjectId);

        return bids.Select(b =>
        {
            projects.TryGetValue(b.ProjectId, out var project);
            ownerNames.TryGetValue(project?.OwnerId ?? Guid.Empty, out var companyName);
            var review = project != null && reviews.TryGetValue(project.Id, out var r) ? r : null;

            return new BidHistoryItemDto
            {
                BidId = b.Id.ToString(),
                ProjectId = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                CompanyName = companyName ?? "Unknown",
                BidAmountJod = b.BidAmountJod,
                Status = BidStatusMapper.ToExternal(b.Status),
                Note = b.Status == BidStatus.Completed ? null : b.Note,
                // Present once the owner has actually left a review — the
                // empty state (completed but not yet reviewed) is just
                // both fields null, no separate flag needed.
                ReviewRating = review != null ? ComputeStars(review) : null,
                ReviewText = review?.Comment
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

    // Legal from Selected, Confirmed with no/rejected guarantee, or
    // InProgress with no work submitted yet — mirrors exactly the four
    // states the UI shows "Back Off" in. Project.AwardedBidId is
    // deliberately left pointing at this bid rather than cleared — the
    // read-side subtitle/detailText logic ("{contractorName} backed off")
    // depends on being able to resolve the bid to get that name. Other
    // bids that were marked NotSelected at Award time are NOT reopened —
    // judgment call carried over from the original Confirm/Back Off pass.
    public async Task<List<MyBidItemDto>?> BackOffBidAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        // Matches exactly the four states the UI offers "Back Off" in —
        // selected, confirmed (no guarantee application yet),
        // guaranteeRejected (latest application rejected), and inProgress
        // with no work submitted yet. Everything else (pending bank
        // review, issued-awaiting-owner-confirm, work already submitted)
        // gets a specific rejection message rather than a generic one.
        GuaranteeApplication? latestGuaranteeApplication = null;

        if (bid.Status == BidStatus.InProgress && bid.WorkSubmittedAt != null)
            throw new InvalidOperationException("Work has already been submitted for this bid and can no longer be backed off.");

        if (bid.Status == BidStatus.Confirmed)
        {
            latestGuaranteeApplication = await _db.GuaranteeApplications
                .Where(g => g.BidId == bid.Id)
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestGuaranteeApplication != null && latestGuaranteeApplication.Status == GuaranteeStatus.PendingBankReview)
                throw new InvalidOperationException("This bid's guarantee is under bank review and can no longer be backed off.");
            if (latestGuaranteeApplication != null && latestGuaranteeApplication.Status == GuaranteeStatus.Issued)
                throw new InvalidOperationException("This bid's guarantee has been issued and can no longer be backed off.");
            if (latestGuaranteeApplication != null && latestGuaranteeApplication.Status != GuaranteeStatus.Rejected)
                throw new InvalidOperationException("This bid can no longer be backed off.");
        }
        else if (bid.Status != BidStatus.PendingConfirmation && bid.Status != BidStatus.InProgress)
        {
            throw new InvalidOperationException("This bid can no longer be backed off.");
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        // Three distinct outcomes: the contractor never confirmed the
        // award (this was a decline, not a withdrawal), withdrew after a
        // guarantee rejection ("After a guarantee rejection" is signalled
        // by the project already sitting in GuaranteeRejectedByYou — that
        // status exists in the schema but nothing sets it yet, guarantee
        // flow isn't built in this pass), or a plain back-off.
        bid.Note = bid.Status == BidStatus.PendingConfirmation
            ? "You declined the award"
            : project.Status == ProjectStatus.GuaranteeRejectedByYou
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

    // Doesn't change Bid.Status — stays InProgress (Completed is reserved
    // for once the owner actually confirms the work, via ReviewWorkService.
    // ConfirmCompleteAsync). Stamps the project's SubmittedDate and flips
    // it to PendingReview, which GET /projects/{id}/submitted-work reads.
    public async Task<List<MyBidItemDto>?> MarkWorkDoneAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        if (bid.Status != BidStatus.InProgress)
            throw new InvalidOperationException("This bid isn't in progress.");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        bid.WorkSubmittedAt = DateTime.UtcNow;
        bid.UpdatedAt = DateTime.UtcNow;

        project.SubmittedDate = DateTime.UtcNow;
        project.Status = ProjectStatus.PendingReview;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetMyBidsAsync(contractorId);
    }

    public async Task<BidDetailDto?> GetBidDetailAsync(Guid contractorId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ContractorId == contractorId);
        if (bid == null) return null;

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId);
        if (project == null) return null;

        var ownerNames = await GetCompanyNamesAsync(new[] { project.OwnerId });
        ownerNames.TryGetValue(project.OwnerId, out var companyName);

        // Only Confirmed/InProgress bids can have one — same scoping as
        // BuildMyBidsAsync. Latest by CreatedAt if more than one exists
        // (a rejected-then-reapplied history).
        var guaranteeApplication = (bid.Status == BidStatus.Confirmed || bid.Status == BidStatus.InProgress)
            ? await _db.GuaranteeApplications
                .Where(g => g.BidId == bid.Id)
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync()
            : null;

        var externalStatus = BidStatusMapper.ToExternal(bid.Status, guaranteeApplication?.Status, bid.WorkSubmittedAt);

        // Only Completed bids can have one — same scoping as GetHistoryAsync.
        var review = bid.Status == BidStatus.Completed
            ? await _db.Reviews.FirstOrDefaultAsync(r => r.ProjectId == project.Id)
            : null;

        return new BidDetailDto
        {
            Id = bid.Id.ToString(),
            ProjectTitle = project.Title,
            CompanyName = companyName ?? "Unknown",
            Status = externalStatus,
            Sector = project.Sector,
            Location = project.Location,
            ContractValue = project.ContractValueJod,
            TimelineRange = project.TimelineText,
            BidAmount = bid.BidAmountJod,
            ProjectId = project.ProjectCode,
            StatusSteps = BuildStatusSteps(bid, guaranteeApplication),

            // Sent regardless of Status — see the comment on BidDetailDto.
            Milestones = string.IsNullOrWhiteSpace(project.Milestones) ? null : project.Milestones,
            GuaranteeTypeRequired = string.IsNullOrWhiteSpace(project.GuaranteeTypeRequired) ? null : project.GuaranteeTypeRequired,
            PaymentTerms = string.IsNullOrWhiteSpace(project.PaymentTerms) ? null : project.PaymentTerms,
            Description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description,

            GuaranteeExpiresInDays = GuaranteeExpiresInDays(bid.Status, guaranteeApplication),
            WorkSubmittedAt = bid.WorkSubmittedAt?.ToString("yyyy-MM-dd"),

            BannerNote = (externalStatus == "REJECTED" || externalStatus == "GUARANTEE_REJECTED")
                ? (bid.Note ?? DefaultActiveNote(bid.Status, guaranteeApplication?.Status))
                : null,

            // Null until the owner's actually left a review — same empty
            // state as BidHistoryItemDto.Review.
            ReviewRating = review != null ? ComputeStars(review) : null,
            ReviewText = review?.Comment
        };
    }

    // GET /api/bids/{bidId}/company-profile. Owner-scoped through the
    // bid's project, not the bid itself — the caller viewing a bidder's
    // profile is the project owner, never the contractor who placed it.
    // A bid that exists but belongs to a project this caller doesn't own
    // 404s the same as a nonexistent bid, same pattern as every other
    // ownership-scoped lookup in this codebase.
    //
    // Every field below is populated unconditionally — see the doc
    // comment on BidderCompanyProfileDto for why that's load-bearing here.
    public async Task<BidderCompanyProfileDto?> GetCompanyProfileAsync(Guid ownerId, Guid bidId)
    {
        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidId);
        if (bid == null) return null;

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == bid.ProjectId && p.OwnerId == ownerId);
        if (project == null) return null;

        var company = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == bid.ContractorId);
        var contractorUser = company == null
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == bid.ContractorId)
            : null;
        var tradingName = company != null
            ? (string.IsNullOrWhiteSpace(company.TradingName) ? company.LegalCompanyName : company.TradingName)
            : (contractorUser?.Name ?? "Unknown Contractor");

        // Real project/review history for this contractor — shared with
        // GET /capability-score/me and GET /company-profile/reviews via
        // ContractorTrackRecordHelper so all three endpoints report the
        // same numbers for the same contractor.
        var (totalProjects, failedProjects, currentProjects) =
            await ContractorTrackRecordHelper.GetProjectStatsAsync(_db, bid.ContractorId);
        var (avgRating, reviewItems) = await ContractorTrackRecordHelper.GetReviewSummaryAsync(_db, bid.ContractorId);

        // Always fresh, same "recalculate then read" pattern as
        // ProjectService.BuildBidderDtoAsync — never throws even if this
        // contractor has no bank connected / no score row yet.
        await _capabilityScoreService.RecalculateAsync(bid.ContractorId);
        var score = await _capabilityScoreService.GetAsync(bid.ContractorId);

        return new BidderCompanyProfileDto
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
            PrimaryEmail = company?.PrimaryEmail ?? contractorUser?.Email ?? string.Empty,
            PrimaryPhoneNumber = company?.PrimaryPhoneNumber ?? contractorUser?.Phone ?? string.Empty,
            BusinessLicenseNumber = company?.BusinessLicenseNumber ?? string.Empty,
            ContractorClassificationGrade = company?.ContractorClassificationGrade ?? string.Empty,
            Sectors = company?.Sectors ?? new List<string>(),
            YearsOfExperience = company != null && company.YearOfEstablishment > 0
                ? Math.Max(0, DateTime.UtcNow.Year - company.YearOfEstablishment)
                : 0,

            TrackRecordStats = new BidderTrackRecordStatsDto
            {
                TotalProjects = totalProjects,
                FailedProjects = failedProjects,
                CurrentProjects = currentProjects,
                AvgRating = Math.Round(avgRating, 1)
            },

            ScoreBreakdown = new BidderScoreBreakdownDto
            {
                FinancialSolvency = (int)Math.Round(new[]
                {
                    score.Factors.NumberOfCurrentDebts.Percentage,
                    score.Factors.DebtCapacity.Percentage,
                    score.Factors.CompanyAssetsValue.Percentage,
                    score.Factors.DelinquentDebts.Percentage,
                    score.Factors.PaymentHistory.Percentage,
                    score.Factors.CashflowTrends.Percentage
                }.Average()),
                ProjectTrackRecord = (int)Math.Round(new[]
                {
                    score.Factors.CurrentWorkload.Percentage,
                    score.Factors.ProjectDeliveryHistory.Percentage
                }.Average()),
                PastProjectRatings = (int)Math.Round(avgRating / 5.0 * 100)
            },

            Reviews = new BidderReviewsSummaryDto
            {
                AverageRating = Math.Round(avgRating, 1),
                TotalReviews = reviewItems.Count,
                Items = reviewItems
            }
        };
    }

    // Four-stage timeline covering the happy path (Submitted -> Selected
    // -> Guarantee -> Complete). Doesn't have a dedicated stage for
    // BackedOff/BACKED_OFF — that status isn't in the frontend's handled
    // enum for this screen, so it's not clear what that screen wants
    // shown there yet; the steps below just describe wherever the bid
    // got to before backing off, same as "rejected" does for NotSelected.
    private static List<BidStatusStepDto> BuildStatusSteps(Bid bid, GuaranteeApplication? guaranteeApplication)
    {
        var submittedStep = new BidStatusStepDto
        {
            Label = "Bid Submitted",
            Date = bid.CreatedAt.ToString("MMM d, yyyy"),
            State = "completed"
        };

        var selectedStep = new BidStatusStepDto { Label = "Selected by Owner" };
        var guaranteeStep = new BidStatusStepDto { Label = "Guarantee Approved" };
        var completeStep = new BidStatusStepDto { Label = "Work Completed" };

        switch (bid.Status)
        {
            case BidStatus.Submitted:
                selectedStep.State = "pending";
                break;

            case BidStatus.PendingConfirmation:
                selectedStep.State = "current";
                break;

            case BidStatus.NotSelected:
                selectedStep.State = "rejected";
                selectedStep.Date = bid.UpdatedAt.ToString("MMM d, yyyy");
                break;

            case BidStatus.Confirmed:
                selectedStep.State = "completed";
                selectedStep.Date = bid.UpdatedAt.ToString("MMM d, yyyy");
                guaranteeStep.State = guaranteeApplication?.Status switch
                {
                    GuaranteeStatus.PendingBankReview => "current",
                    GuaranteeStatus.Issued => "current", // bank issued it; still waiting on the owner's confirm
                    GuaranteeStatus.Rejected => "rejected",
                    _ => "pending" // no application submitted yet
                };
                if (guaranteeApplication != null)
                    guaranteeStep.Date = guaranteeApplication.UpdatedAt.ToString("MMM d, yyyy");
                break;

            case BidStatus.InProgress:
                selectedStep.State = "completed";
                guaranteeStep.State = "completed";
                if (guaranteeApplication != null)
                    guaranteeStep.Date = guaranteeApplication.UpdatedAt.ToString("MMM d, yyyy");
                // Work submitted but not yet confirmed by the owner —
                // "current" rather than leaving it looking untouched.
                // Actually confirmed (bid.Status == Completed) is the
                // case below.
                if (bid.WorkSubmittedAt.HasValue)
                {
                    completeStep.State = "current";
                    completeStep.Date = bid.WorkSubmittedAt.Value.ToString("MMM d, yyyy");
                }
                break;

            case BidStatus.Completed:
                selectedStep.State = "completed";
                guaranteeStep.State = "completed";
                completeStep.State = "completed";
                completeStep.Date = bid.UpdatedAt.ToString("MMM d, yyyy");
                break;

            case BidStatus.BackedOff:
                // Best-effort: reflects that they'd at least been
                // selected before backing off; doesn't distinguish how
                // far past that they got. Revisit once this status is
                // actually in scope for this screen.
                selectedStep.State = "completed";
                break;
        }

        return new List<BidStatusStepDto> { submittedStep, selectedStep, guaranteeStep, completeStep };
    }

    private async Task<List<MyBidItemDto>> BuildMyBidsAsync(List<Bid> bids)
    {
        if (bids.Count == 0) return new List<MyBidItemDto>();

        var projects = await _db.Projects
            .Where(p => bids.Select(b => b.ProjectId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var ownerNames = await GetCompanyNamesAsync(
            bids.Where(b => projects.ContainsKey(b.ProjectId)).Select(b => projects[b.ProjectId].OwnerId));

        // Confirmed bids may have a pending/rejected application (drives
        // the CONFIRMED / GUARANTEE_PENDING_REVIEW / GUARANTEE_REJECTED
        // split above); InProgress bids have an Approved one, whose
        // ValidityExpiry drives GuaranteeExpiresInDays below. No other
        // bid status can have an application (see
        // GuaranteeService.ResolveConfirmedBidAsync).
        var relevantBidIds = bids
            .Where(b => b.Status == BidStatus.Confirmed || b.Status == BidStatus.InProgress)
            .Select(b => b.Id)
            .ToList();
        var guaranteeApplications = relevantBidIds.Count == 0
            ? new Dictionary<Guid, GuaranteeApplication>()
            : await _db.GuaranteeApplications
                .Where(g => relevantBidIds.Contains(g.BidId))
                .GroupBy(g => g.BidId)
                .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        return bids.Select(b =>
        {
            projects.TryGetValue(b.ProjectId, out var project);
            ownerNames.TryGetValue(project?.OwnerId ?? Guid.Empty, out var companyName);
            guaranteeApplications.TryGetValue(b.Id, out var guaranteeApplication);

            return new MyBidItemDto
            {
                BidId = b.Id.ToString(),
                ProjectId = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                CompanyName = companyName ?? "Unknown",
                BidAmountJod = b.BidAmountJod,
                Status = BidStatusMapper.ToExternal(b.Status, guaranteeApplication?.Status, b.WorkSubmittedAt),
                Note = b.Note ?? DefaultActiveNote(b.Status, guaranteeApplication?.Status),
                GuaranteeExpiresInDays = GuaranteeExpiresInDays(b.Status, guaranteeApplication),
                WorkSubmittedAt = b.WorkSubmittedAt?.ToString("yyyy-MM-dd")
            };
        }).ToList();
    }

    // Only meaningful once the guarantee is actually active (InProgress +
    // an Approved application). Floors at 0 rather than going negative if
    // ValidityExpiry has already passed — expiry-driven state transitions
    // (e.g. auto-flagging an expired guarantee) aren't built yet, so an
    // overdue guarantee just reads as "0 days left" for now rather than
    // something misleading like -3.
    private static int? GuaranteeExpiresInDays(string bidStatus, GuaranteeApplication? guaranteeApplication)
    {
        if (bidStatus != BidStatus.InProgress || guaranteeApplication?.Status != GuaranteeStatus.Approved)
            return null;

        var daysLeft = (guaranteeApplication.ValidityExpiry.Date - DateTime.UtcNow.Date).Days;
        return Math.Max(daysLeft, 0);
    }

    // Display text for the active states, when nothing's been explicitly
    // stored on the bid (Note is only ever persisted for terminal states).
    // guaranteeStatus is null until a GuaranteeApplication exists for this
    // bid — see BuildMyBidsAsync.
    private static string? DefaultActiveNote(string status, string? guaranteeStatus) => (status, guaranteeStatus) switch
    {
        (BidStatus.Submitted, _) => "Waiting for owner's decision",
        (BidStatus.PendingConfirmation, _) => "Awarded to you — confirm to proceed",
        (BidStatus.Confirmed, GuaranteeStatus.PendingBankReview) => "Guarantee application submitted — waiting on bank review",
        (BidStatus.Confirmed, GuaranteeStatus.Issued) => "Guarantee issued by the bank — waiting on the owner to confirm",
        (BidStatus.Confirmed, GuaranteeStatus.Rejected) => "Your guarantee application was rejected. Apply for a new one or back off.",
        (BidStatus.Confirmed, _) => "Confirmed — apply for your guarantee to proceed",
        (BidStatus.InProgress, _) => "Work in progress",
        _ => null
    };

    // ReviewRating is a single number; Review stores six separate
    // 1-5 categories (see leave_review_model.dart's ReviewCategory). This
    // is the average, rounded to the nearest whole star — WouldYouRehire
    // included, same as every other category, since the frontend rates
    // it 1-5 too rather than treating it as a yes/no.
    private static int ComputeStars(Review review)
    {
        var average = (review.QualityOfWorkmanship + review.AdherenceToTimeline + review.AdherenceToBudgetScope +
                        review.CommunicationResponsiveness + review.SiteSafetyCompliance + review.WouldYouRehire) / 6.0;
        return (int)Math.Round(average, MidpointRounding.AwayFromZero);
    }

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
// (PendingConfirmation -> SELECTED, BackedOff -> BACKED_OFF, etc.).
public static class BidStatusMapper
{
    // guaranteeStatus is the related GuaranteeApplication's Status (null if
    // no application has been submitted yet for this bid). Only relevant
    // while internalStatus == Confirmed — a bid can't have an application
    // in any other state (see GuaranteeService.ResolveConfirmedBidAsync).
    //
    // workSubmittedAt is Bid.WorkSubmittedAt — only relevant while
    // internalStatus == InProgress. Distinguishes "guarantee active, work
    // underway" (IN_PROGRESS) from "contractor marked it done, waiting on
    // the owner to confirm/flag" (WORK_SUBMITTED), which the bid-side
    // DTOs previously had no way to signal at all.
    //
    // Confirmed splits into four external states so the frontend doesn't
    // have to infer any of this from Note text:
    //   - CONFIRMED: bid confirmed, contractor hasn't applied for the
    //     guarantee yet — "Apply for Guarantee" button state.
    //   - GUARANTEE_PENDING_REVIEW: application submitted, bank hasn't
    //     decided yet.
    //   - GUARANTEE_ISSUED: bank issued the guarantee; waiting on the
    //     project owner to confirm it (the second half of the two-stage
    //     bank -> owner decision — see GuaranteeService.ConfirmAsync).
    //   - GUARANTEE_REJECTED: rejected — either by the bank or by the
    //     owner (see GuaranteeRejectedBy) — "Back Off" / "Apply for New
    //     Guarantee" button state either way. Bid.Status itself stays
    //     Confirmed here (see GuaranteeService.RejectByBankAsync /
    //     RejectByOwnerAsync); this is a read-side-only distinction
    //     driven by the linked GuaranteeApplication's Status.
    // On the owner's confirm, the bid moves to a real BidStatus
    // (InProgress) instead of staying Confirmed, so there's no
    // "GUARANTEE_APPROVED" case here — (BidStatus.InProgress, _) below
    // already covers it.
    public static string ToExternal(string internalStatus, string? guaranteeStatus = null, DateTime? workSubmittedAt = null) =>
        (internalStatus, guaranteeStatus, workSubmittedAt.HasValue) switch
        {
            (BidStatus.Submitted, _, _) => "PENDING",
            (BidStatus.PendingConfirmation, _, _) => "SELECTED",
            (BidStatus.Confirmed, GuaranteeStatus.PendingBankReview, _) => "GUARANTEE_PENDING_REVIEW",
            (BidStatus.Confirmed, GuaranteeStatus.Issued, _) => "GUARANTEE_ISSUED",
            (BidStatus.Confirmed, GuaranteeStatus.Rejected, _) => "GUARANTEE_REJECTED",
            (BidStatus.Confirmed, _, _) => "CONFIRMED",
            (BidStatus.InProgress, _, true) => "WORK_SUBMITTED",
            (BidStatus.InProgress, _, _) => "IN_PROGRESS",
            (BidStatus.NotSelected, _, _) => "REJECTED",
            (BidStatus.BackedOff, _, _) => "BACKED_OFF",
            (BidStatus.Completed, _, _) => "COMPLETED",
            _ => internalStatus.ToUpperInvariant()
        };
}