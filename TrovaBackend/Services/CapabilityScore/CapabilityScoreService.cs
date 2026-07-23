using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrovaBackend.Data;
using TrovaBackend.DTOs.CapabilityScore;
using TrovaBackend.Services.Shared;

namespace TrovaBackend.Services.CapabilityScore;

public interface ICapabilityScoreService
{
    // Called by BankConnectionService right after a bank connects (and
    // later, by anything else that changes a scoring factor — a payment,
    // a project completing, etc. — same event-triggered pattern discussed
    // for the scoring UI). Recomputes and overwrites the stored score.
    Task RecalculateAsync(Guid userId);

    Task<CapabilityScoreResponse> GetAsync(Guid userId);
}

public class CapabilityScoreService : ICapabilityScoreService
{
    private readonly AppDbContext _db;
    private readonly ScoringOptions _options;
    private readonly TrovaBackend.Services.Notifications.INotificationService _notificationService;

    public CapabilityScoreService(AppDbContext db, IOptions<ScoringOptions> options, TrovaBackend.Services.Notifications.INotificationService notificationService)
    {
        _db = db;
        _options = options.Value;
        _notificationService = notificationService;
    }

    public async Task RecalculateAsync(Guid userId)
    {
        var bank = await _db.BankConnections.FirstOrDefaultAsync(b => b.UserId == userId);
        var company = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userId);

        // ── Bank-derived factors ─────────────────────────────────────────
        // If no bank is connected yet, these can't be computed for real —
        // scored as 0 with a description explaining why, rather than a
        // fake number, so the UI can visibly show "not yet connected"
        // instead of silently lying with a placeholder score.
        int currentDebtsScore, debtCapacityScore, assetsScore, delinquentScore, cashflowScore;
        string currentDebtsDesc, debtCapacityDesc, assetsDesc, delinquentDesc, cashflowDesc;

        if (bank == null)
        {
            (currentDebtsScore, currentDebtsDesc) = (0, "Bank not connected yet");
            (debtCapacityScore, debtCapacityDesc) = (0, "Bank not connected yet");
            (assetsScore, assetsDesc) = (0, "Bank not connected yet");
            (delinquentScore, delinquentDesc) = (0, "Bank not connected yet");
            (cashflowScore, cashflowDesc) = (0, "Bank not connected yet");
        }
        else
        {
            currentDebtsScore = Clamp0To100(100 - (bank.NumberOfCurrentDebts * 8));
            currentDebtsDesc = $"{bank.NumberOfCurrentDebts} current debt(s)";

            debtCapacityScore = _options.RecommendedMaxDebtCapacityJod > 0
                ? Clamp0To100((int)Math.Round(100m * bank.RemainingDebtCapacityJod / _options.RecommendedMaxDebtCapacityJod))
                : 0;
            debtCapacityDesc = $"JOD {bank.RemainingDebtCapacityJod:N0} remaining of JOD {_options.RecommendedMaxDebtCapacityJod:N0} recommended max (self-reported)";

            assetsScore = _options.BenchmarkAssetsValueJod > 0
                ? Clamp0To100((int)Math.Round(100m * bank.AvailableBalanceAmount / _options.BenchmarkAssetsValueJod))
                : 0;
            assetsDesc = $"JOD {bank.AvailableBalanceAmount:N0} in verified assets";

            delinquentScore = Clamp0To100(100 - (bank.NumberOfDelinquentDebts * 25));
            delinquentDesc = bank.NumberOfDelinquentDebts == 0
                ? "No delinquent debts (self-reported)"
                : $"{bank.NumberOfDelinquentDebts} delinquent debt(s) (self-reported)";

            cashflowScore = Clamp0To100((int)Math.Round(50 + (bank.AverageMonthlyCashflowChangePercent * 5)));
            cashflowDesc = $"{bank.AverageMonthlyCashflowChangePercent:+0.0;-0.0}% average monthly change";
        }

        // ── Internal factors (Trova's own project/payment history) ──────
        // Payments still don't exist as a feature, so those stay at a
        // clean-slate default. Projects/Bids/Reviews now exist for real —
        // totalProjects/failedProjects/currentProjects/avgRating are
        // computed here via the same shared helper GET
        // /bids/{bidId}/company-profile and GET /company-profile/reviews
        // use, so this endpoint's trackRecordStats stop being the
        // hardcoded zeros they used to be and agree with those two.
        var (totalProjects, failedProjects, activeProjects) =
            await ContractorTrackRecordHelper.GetProjectStatsAsync(_db, userId);
        var (avgRating, _) = await ContractorTrackRecordHelper.GetReviewSummaryAsync(_db, userId);
        var successfulPayments = 0;
        var totalPayments = 0;

        var paymentHistoryScore = totalPayments > 0
            ? Clamp0To100((int)Math.Round(100.0 * successfulPayments / totalPayments))
            : 100;
        var paymentHistoryDesc = totalPayments > 0
            ? $"{successfulPayments} of {totalPayments} payments successful"
            : "No payment history yet";

        var workloadScore = Clamp0To100(100 - (activeProjects * 12));
        var workloadDesc = activeProjects == 0 ? "No active projects" : $"{activeProjects} active project(s)";

        var deliveryScore = Clamp0To100(100 - (failedProjects * 20));
        var deliveryDesc = totalProjects > 0
            ? $"{totalProjects - failedProjects} of {totalProjects} projects completed on time"
            : "No completed projects yet";

        // ── Weighted composite ───────────────────────────────────────────
        var w = _options.Weights;
        var overall = (currentDebtsScore * w.NumberOfCurrentDebts
                      + debtCapacityScore * w.DebtCapacity
                      + assetsScore * w.CompanyAssetsValue
                      + delinquentScore * w.DelinquentDebts
                      + paymentHistoryScore * w.PaymentHistory
                      + workloadScore * w.CurrentWorkload
                      + deliveryScore * w.ProjectDeliveryHistory
                      + cashflowScore * w.CashflowTrends) / 100m;

        var overallRounded = (int)Math.Round(overall);
        var tierLabel = overallRounded >= _options.TierThresholds.Strong ? "Strong Capability"
                       : overallRounded >= _options.TierThresholds.Moderate ? "Moderate Capability"
                       : "Developing Capability";

        var score = await _db.CapabilityScores.FirstOrDefaultAsync(s => s.UserId == userId);
        var isNew = score == null;
        var previousScore = score?.OverallScore;
        score ??= new Models.CapabilityScore { UserId = userId };

        score.OverallScore = overallRounded;
        score.TierLabel = tierLabel;
        score.TotalProjects = totalProjects;
        score.FailedProjects = failedProjects;
        score.CurrentProjects = activeProjects;
        score.AvgRating = avgRating;

        score.NumberOfCurrentDebtsScore = currentDebtsScore;
        score.NumberOfCurrentDebtsDescription = currentDebtsDesc;
        score.DebtCapacityScore = debtCapacityScore;
        score.DebtCapacityDescription = debtCapacityDesc;
        score.CompanyAssetsValueScore = assetsScore;
        score.CompanyAssetsValueDescription = assetsDesc;
        score.DelinquentDebtsScore = delinquentScore;
        score.DelinquentDebtsDescription = delinquentDesc;
        score.PaymentHistoryScore = paymentHistoryScore;
        score.PaymentHistoryDescription = paymentHistoryDesc;
        score.CurrentWorkloadScore = workloadScore;
        score.CurrentWorkloadDescription = workloadDesc;
        score.ProjectDeliveryHistoryScore = deliveryScore;
        score.ProjectDeliveryHistoryDescription = deliveryDesc;
        score.CashflowTrendsScore = cashflowScore;
        score.CashflowTrendsDescription = cashflowDesc;
        score.LastCalculatedAt = DateTime.UtcNow;

        if (isNew)
            _db.CapabilityScores.Add(score);

        await _db.SaveChangesAsync();

        if (!isNew && previousScore.HasValue && overallRounded > previousScore.Value)
        {
            try
            {
                await _notificationService.CreateAsync(
                    userId,
                    Models.NotificationType.ScoreIncreased,
                    $"Your Capability Score increased by {overallRounded - previousScore.Value} points");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATION FAILED] SCORE_INCREASED for {userId} — {ex.Message}");
            }
        }
    }

    public async Task<CapabilityScoreResponse> GetAsync(Guid userId)
    {
        var score = await _db.CapabilityScores.FirstOrDefaultAsync(s => s.UserId == userId)
            ?? throw new KeyNotFoundException("Capability score has not been calculated yet — connect a bank account first.");

        var company = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userId);

        return new CapabilityScoreResponse
        {
            OverallScore = score.OverallScore,
            TierLabel = score.TierLabel,
            Classification = new DTOs.CapabilityScore.ClassificationDto
            {
                Code = company?.ClassificationCode ?? string.Empty,
                Label = company?.ClassificationLabel ?? string.Empty
            },
            TrackRecordStats = new TrackRecordStatsDto
            {
                TotalProjects = score.TotalProjects,
                FailedProjects = score.FailedProjects,
                CurrentProjects = score.CurrentProjects,
                AvgRating = score.AvgRating
            },
            Factors = new ScoreFactorsDto
            {
                NumberOfCurrentDebts = new ScoreFactorDto { Percentage = score.NumberOfCurrentDebtsScore, Description = score.NumberOfCurrentDebtsDescription },
                DebtCapacity = new ScoreFactorDto { Percentage = score.DebtCapacityScore, Description = score.DebtCapacityDescription },
                CompanyAssetsValue = new ScoreFactorDto { Percentage = score.CompanyAssetsValueScore, Description = score.CompanyAssetsValueDescription },
                DelinquentDebts = new ScoreFactorDto { Percentage = score.DelinquentDebtsScore, Description = score.DelinquentDebtsDescription },
                PaymentHistory = new ScoreFactorDto { Percentage = score.PaymentHistoryScore, Description = score.PaymentHistoryDescription },
                CurrentWorkload = new ScoreFactorDto { Percentage = score.CurrentWorkloadScore, Description = score.CurrentWorkloadDescription },
                ProjectDeliveryHistory = new ScoreFactorDto { Percentage = score.ProjectDeliveryHistoryScore, Description = score.ProjectDeliveryHistoryDescription },
                CashflowTrends = new ScoreFactorDto { Percentage = score.CashflowTrendsScore, Description = score.CashflowTrendsDescription }
            }
        };
    }

    private static int Clamp0To100(int value) => Math.Max(0, Math.Min(100, value));
}
