using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Projects;
using TrovaBackend.Models;
using TrovaBackend.Services.CapabilityScore;

namespace TrovaBackend.Services.Projects;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly ICapabilityScoreService _capabilityScoreService;

    public ProjectService(AppDbContext db, ICapabilityScoreService capabilityScoreService)
    {
        _db = db;
        _capabilityScoreService = capabilityScoreService;
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

    // ── My Projects / History / Detail ──────────────────────────────────────

    private static readonly string[] ActiveStatuses =
    {
        ProjectStatus.OpenForBids, ProjectStatus.Awarded, ProjectStatus.ContractorBackedOff,
        ProjectStatus.GuaranteeRejectedByYou, ProjectStatus.InProgress, ProjectStatus.PendingReview
    };

    private static readonly string[] HistoryStatuses =
    {
        ProjectStatus.Completed, ProjectStatus.Disputed, ProjectStatus.Failed,
        // Set by RepostProjectService once the owner reposts — the
        // original drops out of the active list but stays visible here
        // rather than disappearing outright.
        ProjectStatus.Cancelled
    };

    public async Task<List<ProjectListItemDto>> GetMyProjectsAsync(Guid ownerId)
    {
        var projects = await _db.Projects
            .Where(p => p.OwnerId == ownerId && ActiveStatuses.Contains(p.Status))
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        if (projects.Count == 0) return new List<ProjectListItemDto>();

        var bidCounts = await GetBidCountsAsync(projects.Select(p => p.Id));
        var (awardedBids, contractorNames, classifications) = await GetAwardedBidContextAsync(projects);
        var guaranteeApps = await GetLatestGuaranteeApplicationsAsync(projects.Select(p => p.Id));

        return projects.Select(p =>
        {
            bidCounts.TryGetValue(p.Id, out var bidCount);
            var awardedBid = ResolveAwardedBid(p, awardedBids);
            var contractorName = ResolveContractorName(awardedBid, contractorNames);
            var classification = ResolveClassification(awardedBid, classifications);
            guaranteeApps.TryGetValue(p.Id, out var guaranteeApp);
            var fields = BuildActiveListFields(p.Status, bidCount, awardedBid, contractorName, classification, guaranteeApp);
            var strip = BuildGuaranteeStrip(p.Status, guaranteeApp);

            return new ProjectListItemDto
            {
                ProjectId = p.ProjectCode,
                Title = p.Title,
                Status = p.Status.ToUpperInvariant(),
                ContractValueJod = p.ContractValueJod,
                DetailText = fields.DetailText,
                AwardedBidder = fields.AwardedBidder,
                GuaranteeStripLabel = strip.Label,
                GuaranteeStripSubtext = strip.Subtext,
                GuaranteeStripTone = strip.Tone,
                GuaranteeStatus = ExternalGuaranteeStatus(guaranteeApp?.Status),
                Note = null,
                ActionLabel = fields.ActionLabel
            };
        }).ToList();
    }

    public async Task<List<ProjectHistoryItemDto>> GetMyProjectHistoryAsync(Guid ownerId)
    {
        var projects = await _db.Projects
            .Where(p => p.OwnerId == ownerId && HistoryStatuses.Contains(p.Status))
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        if (projects.Count == 0) return new List<ProjectHistoryItemDto>();

        var (awardedBids, contractorNames, classifications) = await GetAwardedBidContextAsync(projects);

        return projects.Select(p =>
        {
            var awardedBid = ResolveAwardedBid(p, awardedBids);
            var contractorName = ResolveContractorName(awardedBid, contractorNames);

            // Completion date and dispute/failure detail aren't tracked in
            // dedicated columns yet (no ProjectStatusHistory table) — using
            // UpdatedAt as a proxy for "when this status was reached" and
            // the awarded contractor's name where we have one.
            string? detailText = p.Status switch
            {
                ProjectStatus.Completed => $"Completed {p.UpdatedAt:MMMM yyyy}",
                ProjectStatus.Disputed or ProjectStatus.Failed when contractorName != null
                    => $"Contractor: {contractorName}",
                ProjectStatus.Cancelled => $"Reposted {p.UpdatedAt:MMMM yyyy}",
                _ => null
            };

            // Only the Disputed/Failed "Contractor: X" copy names a
            // specific contractor — Completed/Cancelled detail text doesn't.
            var namesContractor = p.Status is ProjectStatus.Disputed or ProjectStatus.Failed && contractorName != null;
            var awardedBidder = namesContractor
                ? BuildAwardedBidder(awardedBid, contractorName, ResolveClassification(awardedBid, classifications))
                : null;

            return new ProjectHistoryItemDto
            {
                ProjectId = p.ProjectCode,
                Title = p.Title,
                Status = p.Status.ToUpperInvariant(),
                ContractValueJod = p.ContractValueJod,
                DetailText = detailText,
                AwardedBidder = awardedBidder,
                // Dispute/guarantee-claim detail isn't modelled yet either —
                // null until that data exists.
                GuaranteeStripLabel = null,
                GuaranteeStripSubtext = null
            };
        }).ToList();
    }

    public async Task<ProjectDetailDto?> GetProjectDetailAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);

        if (project == null) return null;

        Bid? awardedBid = null;
        string? contractorName = null;
        string? classification = null;

        if (project.AwardedBidId.HasValue)
        {
            awardedBid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == project.AwardedBidId.Value);
            if (awardedBid != null)
            {
                var names = await GetContractorNamesAsync(new[] { awardedBid.ContractorId });
                names.TryGetValue(awardedBid.ContractorId, out contractorName);
                var classifications = await GetContractorClassificationsAsync(new[] { awardedBid.ContractorId });
                classifications.TryGetValue(awardedBid.ContractorId, out classification);
            }
        }

        var bidCount = await _db.Bids.CountAsync(b => b.ProjectId == project.Id);
        var guaranteeApp = await _db.GuaranteeApplications
            .Where(g => g.ProjectId == project.Id)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();
        var (statusLabel, actionLabel, actionIsDanger) = GetStatusMeta(project.Status);

        // GetStatusMeta doesn't know about GuaranteeApplication state —
        // this is the one place that context gets layered on top of the
        // generic per-status action label.
        // Same fix as the three copy-builders above: the owner's action is
        // only available once the bank has issued it, not while it's
        // still pending the bank's own decision.
        if (project.Status == ProjectStatus.Awarded && guaranteeApp?.Status == GuaranteeStatus.Issued)
        {
            actionLabel = "Review Guarantee";
        }

        return new ProjectDetailDto
        {
            ProjectId = project.ProjectCode,
            Title = project.Title,
            Status = project.Status.ToUpperInvariant(),
            StatusLabel = statusLabel,
            Subtitle = BuildSubtitle(project.Status, contractorName),
            AwardedBidder = BuildAwardedBidder(awardedBid, contractorName, classification),
            Sector = project.Sector,
            ContractValueJod = project.ContractValueJod,
            Location = project.Location,
            TimelineText = project.TimelineText,
            Milestones = project.Milestones,
            GuaranteeTypeRequired = project.GuaranteeTypeRequired,
            PaymentTerms = project.PaymentTerms,
            BiddersRowText = project.Status == ProjectStatus.OpenForBids
                ? $"{bidCount} bidder{(bidCount == 1 ? "" : "s")}"
                : null,
            GuaranteeRowText = BuildGuaranteeRowText(project.Status, awardedBid, guaranteeApp),
            GuaranteeStatus = ExternalGuaranteeStatus(guaranteeApp?.Status),
            Timeline = BuildTimeline(project, awardedBid),
            ActionLabel = actionLabel,
            ActionIsDanger = actionIsDanger
        };
    }

    // ── Computed-field helpers ───────────────────────────────────────────────

    private async Task<Dictionary<Guid, int>> GetBidCountsAsync(IEnumerable<Guid> projectIds)
    {
        var ids = projectIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await _db.Bids
            .Where(b => ids.Contains(b.ProjectId))
            .GroupBy(b => b.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);
    }

    private async Task<(Dictionary<Guid, Bid> awardedBids, Dictionary<Guid, string> contractorNames, Dictionary<Guid, string> classifications)>
        GetAwardedBidContextAsync(List<Project> projects)
    {
        var awardedBidIds = projects.Where(p => p.AwardedBidId.HasValue)
            .Select(p => p.AwardedBidId!.Value).Distinct().ToList();

        if (awardedBidIds.Count == 0)
            return (new Dictionary<Guid, Bid>(), new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        var awardedBids = await _db.Bids
            .Where(b => awardedBidIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b);

        var contractorNames = await GetContractorNamesAsync(awardedBids.Values.Select(b => b.ContractorId));
        var classifications = await GetContractorClassificationsAsync(awardedBids.Values.Select(b => b.ContractorId));

        return (awardedBids, contractorNames, classifications);
    }

    private static Bid? ResolveAwardedBid(Project p, Dictionary<Guid, Bid> awardedBids) =>
        p.AwardedBidId.HasValue && awardedBids.TryGetValue(p.AwardedBidId.Value, out var bid) ? bid : null;

    private static string? ResolveContractorName(Bid? awardedBid, Dictionary<Guid, string> contractorNames) =>
        awardedBid != null && contractorNames.TryGetValue(awardedBid.ContractorId, out var name) ? name : null;

    private static string? ResolveClassification(Bid? awardedBid, Dictionary<Guid, string> classifications) =>
        awardedBid != null && classifications.TryGetValue(awardedBid.ContractorId, out var code) ? code : null;

    // Join key for the frontend's tap-through to
    // GET /bids/{bidId}/company-profile — see awardedBidder spec. Null
    // when there's no awarded bid or no resolved company name, so the
    // caller can render plain, non-tappable text instead.
    private static AwardedBidderDto? BuildAwardedBidder(Bid? awardedBid, string? contractorName, string? classification)
    {
        if (awardedBid == null || contractorName == null) return null;

        return new AwardedBidderDto
        {
            BidId = awardedBid.Id.ToString(),
            CompanyName = contractorName,
            Classification = classification ?? string.Empty,
            Eligible = true
        };
    }

    // Contractor's display name — trading name if set, else legal company
    // name, falling back to the user's own name if they haven't submitted
    // Company Details yet.
    private async Task<Dictionary<Guid, string>> GetContractorNamesAsync(IEnumerable<Guid> contractorIds)
    {
        var ids = contractorIds.Distinct().ToList();
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

    // Classification code ("A"/"B"/"C") per contractor, keyed the same
    // way as GetContractorNamesAsync — "" (not present in the dictionary)
    // for a contractor who hasn't submitted Company Details yet.
    private async Task<Dictionary<Guid, string>> GetContractorClassificationsAsync(IEnumerable<Guid> contractorIds)
    {
        var ids = contractorIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _db.CompanyDetails
            .Where(c => ids.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId, c => c.ClassificationCode);
    }

    // Latest (most recent) GuaranteeApplication per project — a rejected
    // application can be followed by a fresh resubmission after the
    // contractor applies again, and only the newest one is "live" for
    // badges/action labels. Grouping by ProjectId rather than BidId since
    // that's what every caller here already has on hand.
    private async Task<Dictionary<Guid, GuaranteeApplication>> GetLatestGuaranteeApplicationsAsync(IEnumerable<Guid> projectIds)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, GuaranteeApplication>();

        var apps = await _db.GuaranteeApplications
            .Where(g => ids.Contains(g.ProjectId))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return apps.GroupBy(g => g.ProjectId).ToDictionary(g => g.Key, g => g.First());
    }

    private static (string? DetailText, string? ActionLabel, AwardedBidderDto? AwardedBidder) BuildActiveListFields(
        string status, int bidCount, Bid? awardedBid, string? contractorName, string? classification, GuaranteeApplication? guaranteeApp)
    {
        // Only built (and only ever attached below) when contractorName is
        // non-null — the "contractor"/"Contractor" fallback text used for
        // a null name never names anyone specific, so it never gets a
        // tap-through target either.
        var bidder = BuildAwardedBidder(awardedBid, contractorName, classification);

        return status switch
        {
            ProjectStatus.OpenForBids =>
                ($"{bidCount} bidder{(bidCount == 1 ? "" : "s")}", null, null),

            // "Ready for review" is only true once the BANK has issued
            // it — that's when it's actually the owner's turn to look at
            // it. Previously this checked PendingBankReview, a holdover
            // from when the owner stood in for the bank; now that the
            // bank decides first, PendingBankReview means "still waiting
            // on the bank," not "ready for you."
            ProjectStatus.Awarded when guaranteeApp?.Status == GuaranteeStatus.Issued =>
                ($"Awarded to {contractorName ?? "contractor"} — guarantee ready for review", "Review Guarantee", bidder),

            ProjectStatus.Awarded when guaranteeApp?.Status == GuaranteeStatus.PendingBankReview =>
                ($"Awarded to {contractorName ?? "contractor"} — guarantee pending bank review", null, bidder),

            ProjectStatus.Awarded when awardedBid?.Status == BidStatus.Confirmed =>
                ($"Awarded to {contractorName ?? "contractor"} — waiting for guarantee", null, bidder),

            ProjectStatus.Awarded =>
                ($"Awarded to {contractorName ?? "contractor"} — awaiting confirmation", null, bidder),

            ProjectStatus.ContractorBackedOff =>
                ($"{contractorName ?? "Contractor"} backed off", "Post Project Again", bidder),

            ProjectStatus.GuaranteeRejectedByYou =>
                ("Guarantee rejected", "Post Project Again", null),

            ProjectStatus.InProgress =>
                (contractorName != null ? $"Awarded to {contractorName}" : null, null, bidder),

            ProjectStatus.PendingReview =>
                ("Pending review", "Review Work", null),

            _ => (null, null, null)
        };
    }

    // Colored strip on the My Projects card — only ever shown for the two
    // states that have a guarantee worth calling out (see
    // GuaranteeStripTone in my_projects_model.dart: SUCCESS | WARNING).
    private static (string? Label, string? Subtext, string? Tone) BuildGuaranteeStrip(
        string status, GuaranteeApplication? guaranteeApp)
    {
        if (guaranteeApp == null) return (null, null, null);

        return (status, guaranteeApp.Status) switch
        {
            // Same fix as BuildActiveListFields above: "ready for review"
            // only applies once the bank has actually issued it.
            (ProjectStatus.Awarded, GuaranteeStatus.Issued) =>
                ("Guarantee: Ready for Review", "Bank has issued it", "WARNING"),

            (ProjectStatus.Awarded, GuaranteeStatus.PendingBankReview) =>
                ("Guarantee: Pending Bank Review", "Waiting on bank decision", "WARNING"),

            (ProjectStatus.InProgress, GuaranteeStatus.Approved) =>
                ("Guarantee: Active", $"Expires {guaranteeApp.ValidityExpiry:MMM yyyy}", "SUCCESS"),

            _ => (null, null, null)
        };
    }

    // Maps internal GuaranteeApplication.Status onto the same external
    // vocabulary OwnerGuaranteeDto.Status uses (GuaranteeService's
    // ExternalGuaranteeStatus) — kept as its own small copy here rather
    // than reaching into GuaranteeService, same "each service maps its
    // own status vocabulary" pattern BidStatusMapper already follows.
    // Null in, null out — no guarantee application exists yet.
    private static string? ExternalGuaranteeStatus(string? status) => status switch
    {
        null => null,
        GuaranteeStatus.PendingBankReview => "PENDING_REVIEW",
        GuaranteeStatus.Issued => "ISSUED",
        GuaranteeStatus.Approved => "ACTIVE",
        GuaranteeStatus.Rejected => "REJECTED",
        _ => status.ToUpperInvariant(),
    };

    // Project Detail's single guarantee row. Keeps the original "awaiting
    // issuance" fallback for when the contractor's confirmed but hasn't
    // submitted an application yet, and adds the two guarantee-app-driven
    // cases on top.
    private static string? BuildGuaranteeRowText(string status, Bid? awardedBid, GuaranteeApplication? guaranteeApp)
    {
        if (status == ProjectStatus.Awarded && awardedBid?.Status == BidStatus.Confirmed)
        {
            // Same fix as BuildActiveListFields/BuildGuaranteeStrip: "ready
            // for your review" only applies once the bank has issued it —
            // PendingBankReview means still waiting on the bank, not you.
            return guaranteeApp?.Status switch
            {
                GuaranteeStatus.Issued => "Ready for your review",
                GuaranteeStatus.PendingBankReview => "Pending bank review",
                _ => "Awaiting guarantee issuance"
            };
        }

        if (status == ProjectStatus.InProgress && guaranteeApp?.Status == GuaranteeStatus.Approved)
            return $"Active · Expires {guaranteeApp.ValidityExpiry:MMM yyyy}";

        return null;
    }

    private static (string Label, string? ActionLabel, bool ActionIsDanger) GetStatusMeta(string status) =>
        status switch
        {
            ProjectStatus.OpenForBids => ("Open for Bids", "View Bidders", false),
            ProjectStatus.Awarded => ("Awarded", null, false),
            ProjectStatus.ContractorBackedOff => ("Contractor Backed Off", "Post Project Again", false),
            ProjectStatus.GuaranteeRejectedByYou => ("Guarantee Rejected", "Post Project Again", false),
            ProjectStatus.InProgress => ("In Progress", null, false),
            ProjectStatus.PendingReview => ("Pending Review", "Review Work", false),
            ProjectStatus.Completed => ("Completed", null, false),
            ProjectStatus.Disputed => ("Disputed", "View Dispute Status", false),
            ProjectStatus.Failed => ("Failed", "View Guarantee Claim", true),
            ProjectStatus.Cancelled => ("Reposted", null, false),
            _ => (status, null, false)
        };

    private static string? BuildSubtitle(string status, string? contractorName)
    {
        if (contractorName == null) return null;

        return status switch
        {
            ProjectStatus.ContractorBackedOff => $"{contractorName} backed off",
            ProjectStatus.GuaranteeRejectedByYou => $"Guarantee rejected — {contractorName}",
            _ => $"Awarded to {contractorName}"
        };
    }

    // Dates beyond "Posted" are null — there's no ProjectStatusHistory table
    // tracking when each stage was actually reached yet, so we only assert
    // ordering/state (DONE/ACTIVE/UPCOMING/FAILED), not timestamps. Add real
    // dates once that table exists.
    private static List<TimelineStepDto> BuildTimeline(Project project, Bid? awardedBid)
    {
        var posted = new TimelineStepDto
        {
            Label = "Posted",
            Date = project.CreatedAt.ToString("yyyy-MM-dd"),
            State = "DONE"
        };

        switch (project.Status)
        {
            case ProjectStatus.OpenForBids:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "ACTIVE"),
                    Step("In Progress", "UPCOMING"),
                    Step("Pending Review → Completed", "UPCOMING"),
                };

            case ProjectStatus.Awarded:
                var confirmed = awardedBid?.Status == BidStatus.Confirmed;
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Active", confirmed ? "ACTIVE" : "UPCOMING"),
                    Step("In Progress", "UPCOMING"),
                    Step("Pending Review → Completed", "UPCOMING"),
                };

            case ProjectStatus.ContractorBackedOff:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "FAILED"),
                };

            case ProjectStatus.GuaranteeRejectedByYou:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Active", "FAILED"),
                };

            case ProjectStatus.InProgress:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Active", "DONE"),
                    Step("In Progress", "ACTIVE"),
                    Step("Pending Review → Completed", "UPCOMING"),
                };

            case ProjectStatus.PendingReview:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Active", "DONE"),
                    Step("In Progress", "DONE"),
                    Step("Pending Review", "ACTIVE"),
                    Step("Completed", "UPCOMING"),
                };

            case ProjectStatus.Completed:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Active", "DONE"),
                    Step("In Progress", "DONE"),
                    Step("Pending Review", "DONE"),
                    new TimelineStepDto { Label = "Completed", Date = project.UpdatedAt.ToString("yyyy-MM-dd"), State = "DONE" },
                };

            case ProjectStatus.Disputed:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("In Progress", "DONE"),
                    Step("Disputed", "FAILED"),
                };

            case ProjectStatus.Failed:
                return new List<TimelineStepDto>
                {
                    posted,
                    Step("Awarded (Contract Signed)", "DONE"),
                    Step("Guarantee Claim", "FAILED"),
                };

            default:
                return new List<TimelineStepDto> { posted };
        }

        static TimelineStepDto Step(string label, string state) =>
            new() { Label = label, Date = null, State = state };
    }

    // ── Bidders / Compare / Award ────────────────────────────────────────────

    public async Task<List<BidderDto>?> GetProjectBiddersAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null) return null;

        var bids = await _db.Bids
            .Where(b => b.ProjectId == project.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        if (bids.Count == 0) return new List<BidderDto>();

        var result = new List<BidderDto>(bids.Count);
        foreach (var bid in bids)
        {
            var bidder = await BuildBidderDtoAsync(bid, project);
            result.Add(bidder);
        }

        // Highest capability score first — this is the compare screen's
        // primary sort signal.
        return result.OrderByDescending(b => b.CapabilityScore).ToList();
    }

    public async Task<AwardProjectResponse?> AwardProjectAsync(Guid ownerId, string projectId, AwardProjectRequest request)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null) return null;

        if (project.Status != ProjectStatus.OpenForBids)
            throw new InvalidOperationException("This project is not open for bids and cannot be awarded.");

        if (!Guid.TryParse(request.BidId, out var bidGuid))
            throw new ArgumentException("BidId is not a valid identifier.");

        var winningBid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == bidGuid && b.ProjectId == project.Id);
        if (winningBid == null)
            throw new InvalidOperationException("This bid does not belong to this project.");

        // Re-validate eligibility server-side — never trust a client-supplied
        // eligible flag, and this also gates whether Award can succeed at all.
        var bidder = await BuildBidderDtoAsync(winningBid, project);
        if (!bidder.Eligible)
            throw new InvalidOperationException("This bid does not meet the project's minimum score/classification requirements.");

        project.Status = ProjectStatus.Awarded;
        project.AwardedBidId = winningBid.Id;
        project.UpdatedAt = DateTime.UtcNow;

        winningBid.Status = BidStatus.PendingConfirmation;
        winningBid.UpdatedAt = DateTime.UtcNow;

        // Every other bid on this project is no longer live once one is awarded.
        var otherBids = await _db.Bids
            .Where(b => b.ProjectId == project.Id && b.Id != winningBid.Id)
            .ToListAsync();

        foreach (var other in otherBids)
        {
            other.Status = BidStatus.NotSelected;
            other.Note = "Owner selected a different bidder";
            other.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return new AwardProjectResponse
        {
            ProjectId = project.ProjectCode,
            Status = project.Status.ToUpperInvariant(),
            AwardedCompanyName = bidder.CompanyName
        };
    }

    // Shared by the bidders list and the award re-validation, so eligibility
    // can never drift between "what the owner saw" and "what Award enforces."
    private async Task<BidderDto> BuildBidderDtoAsync(Bid bid, Project project)
    {
        // Always fresh — reuses the same rule-based engine as
        // GET /api/capability-score/me, not reimplemented here.
        await _capabilityScoreService.RecalculateAsync(bid.ContractorId);
        var score = await _capabilityScoreService.GetAsync(bid.ContractorId);

        var company = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == bid.ContractorId);
        var companyName = company != null
            ? (string.IsNullOrWhiteSpace(company.TradingName) ? company.LegalCompanyName : company.TradingName)
            : (await _db.Users.FirstOrDefaultAsync(u => u.Id == bid.ContractorId))?.Name ?? "Unknown Contractor";

        var classificationCode = company?.ClassificationCode ?? string.Empty;

        var eligible = score.OverallScore >= project.MinimumRequiredScore
            && ClassificationRank(classificationCode) >= ClassificationRank(project.MinimumClassification);

        return new BidderDto
        {
            BidId = bid.Id.ToString(),
            CompanyName = companyName,
            CapabilityScore = score.OverallScore,
            BidAmountJod = bid.BidAmountJod,
            Classification = classificationCode,
            Eligible = eligible,
            SubFactors = new BidSubFactorsDto
            {
                CurrentDebts = score.Factors.NumberOfCurrentDebts.Percentage,
                DebtCapacity = score.Factors.DebtCapacity.Percentage,
                AssetsValue = score.Factors.CompanyAssetsValue.Percentage,
                DelinquentDebts = score.Factors.DelinquentDebts.Percentage,
                PaymentHistory = score.Factors.PaymentHistory.Percentage,
                CurrentWorkload = score.Factors.CurrentWorkload.Percentage,
                DeliveryHistory = score.Factors.ProjectDeliveryHistory.Percentage,
                CashflowTrends = score.Factors.CashflowTrends.Percentage
            }
        };
    }

    // "A" beats a minimum of "B" or "C"; unranked/missing classification
    // (contractor hasn't submitted Company Details) never clears any bar.
    private static int ClassificationRank(string code) => code switch
    {
        "A" => 3,
        "B" => 2,
        "C" => 1,
        _ => 0
    };

    // ── Browse Projects / Detail / Submit Bid (contractor side) ─────────────

    public async Task<List<BrowseProjectListItemDto>> BrowseProjectsAsync(
        List<string>? sectors, decimal? minValue, decimal? maxValue)
    {
        var query = _db.Projects.Where(p => p.Status == ProjectStatus.OpenForBids);

        if (sectors is { Count: > 0 })
            query = query.Where(p => sectors.Contains(p.Sector));

        if (minValue.HasValue)
            query = query.Where(p => p.ContractValueJod >= minValue.Value);

        if (maxValue.HasValue)
            query = query.Where(p => p.ContractValueJod <= maxValue.Value);

        var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        if (projects.Count == 0) return new List<BrowseProjectListItemDto>();

        // Same helper used for awarded-contractor names elsewhere — generic
        // by userId, works just as well for project owners.
        var ownerNames = await GetContractorNamesAsync(projects.Select(p => p.OwnerId));

        return projects.Select(p => new BrowseProjectListItemDto
        {
            ProjectId = p.ProjectCode,
            Title = p.Title,
            PostedByCompanyName = ownerNames.TryGetValue(p.OwnerId, out var name) ? name : "Unknown",
            Sector = p.Sector,
            ContractValueJod = p.ContractValueJod,
            MinimumRequiredScore = p.MinimumRequiredScore,
            MinimumClassification = p.MinimumClassification,
            DaysLeftText = BuildDaysLeftText(p.BidSubmissionDeadline)
        }).ToList();
    }

    public async Task<BrowseProjectDetailDto?> GetBrowseProjectDetailAsync(Guid contractorId, string projectId)
    {
        // Browse only ever surfaces open projects — a project that's since
        // been awarded/closed 404s here the same as one that never existed,
        // consistent with how the owner-side Detail endpoint scopes 404s.
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.ProjectCode == projectId && p.Status == ProjectStatus.OpenForBids);
        if (project == null) return null;

        var ownerNames = await GetContractorNamesAsync(new[] { project.OwnerId });
        ownerNames.TryGetValue(project.OwnerId, out var ownerName);

        var alreadyBid = await _db.Bids.AnyAsync(b => b.ProjectId == project.Id && b.ContractorId == contractorId);

        return new BrowseProjectDetailDto
        {
            ProjectId = project.ProjectCode,
            Title = project.Title,
            PostedByCompanyName = ownerName ?? "Unknown",
            Sector = project.Sector,
            Location = project.Location,
            ContractValueJod = project.ContractValueJod,
            TimelineText = project.TimelineText,
            Milestones = project.Milestones,
            GuaranteeTypeRequired = project.GuaranteeTypeRequired,
            PaymentTerms = project.PaymentTerms,
            MinimumRequiredScore = project.MinimumRequiredScore,
            MinimumClassification = project.MinimumClassification,
            MinimumClassificationText = BuildMinimumClassificationText(project.MinimumClassification),
            BidDeadlineText = project.BidSubmissionDeadline.ToString("MMMM d, yyyy"),
            Description = project.Description,
            AlreadyBid = alreadyBid
        };
    }

    public async Task<SubmitBidResponse> SubmitBidAsync(Guid contractorId, string projectId, SubmitBidRequest request)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found.");

        if (project.Status != ProjectStatus.OpenForBids)
            throw new InvalidOperationException("This project is no longer open for bids.");

        var alreadyBid = await _db.Bids.AnyAsync(b => b.ProjectId == project.Id && b.ContractorId == contractorId);
        if (alreadyBid)
            throw new InvalidOperationException("You have already submitted a bid for this project.");

        // Eligibility is deliberately NOT enforced here — the Submit Bid
        // screen's own disclaimer says the score becomes visible to the
        // owner for evaluation, not that it gates submission. The hard
        // eligibility gate lives at Award time (AwardProjectAsync), where
        // it's re-validated server-side regardless of what this bid looked
        // like when submitted.
        var bid = new Bid
        {
            ProjectId = project.Id,
            ContractorId = contractorId,
            BidAmountJod = request.BidAmountJod,
            Status = BidStatus.Submitted
        };

        _db.Bids.Add(bid);
        await _db.SaveChangesAsync();

        return new SubmitBidResponse
        {
            BidId = bid.Id.ToString(),
            ProjectId = project.ProjectCode,
            Status = bid.Status.ToUpperInvariant()
        };
    }

    private static string BuildDaysLeftText(DateTime deadlineUtc)
    {
        var days = (int)Math.Ceiling((deadlineUtc - DateTime.UtcNow).TotalDays);
        return days switch
        {
            < 0 => "Deadline passed",
            0 => "Last day to bid",
            1 => "1 day left",
            _ => $"{days} days left"
        };
    }

    private static string BuildMinimumClassificationText(string code) => code switch
    {
        "A" => "Class A",
        "B" => "Class B or higher",
        "C" => "Class C or higher",
        _ => code
    };
}