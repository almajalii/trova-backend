using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Guarantees;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Guarantees;

public class GuaranteeService : IGuaranteeService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB, per spec

    // Translates the Dart enum's camelCase .name values onto the internal
    // SCREAMING_SNAKE vocabulary. See the comment on Models.GuaranteeTypes
    // for why this mapping exists instead of storing the raw value.
    private static readonly Dictionary<string, string> GuaranteeTypeFromFrontend =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["performance"] = GuaranteeTypes.Performance,
            ["bidBond"] = GuaranteeTypes.BidBond,
            ["advancePayment"] = GuaranteeTypes.AdvancePayment,
            ["retention"] = GuaranteeTypes.Retention,
        };

    private readonly AppDbContext _db;
    private readonly GuaranteeStorageOptions _storageOptions;
    private readonly string _contentRootPath;

    public GuaranteeService(
        AppDbContext db,
        IOptions<GuaranteeStorageOptions> storageOptions,
        IHostEnvironment hostEnvironment)
    {
        _db = db;
        _storageOptions = storageOptions.Value;
        _contentRootPath = hostEnvironment.ContentRootPath;
    }

    // ── Prefill ──────────────────────────────────────────────────────────

    public async Task<GuaranteePrefillResponse> GetPrefillAsync(Guid contractorId, string projectId)
    {
        var (project, bid) = await ResolveConfirmedBidAsync(contractorId, projectId);

        var applicantCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == contractorId);
        if (applicantCompany == null)
            throw new InvalidOperationException("Please complete your Company Details before applying for a guarantee.");

        var beneficiaryCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == project.OwnerId);
        var beneficiaryUser = beneficiaryCompany == null
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == project.OwnerId)
            : null;

        var contractorCode = await GetOrCreatePublicCodeAsync(applicantCompany);
        var beneficiaryCode = beneficiaryCompany != null
            ? await GetOrCreatePublicCodeAsync(beneficiaryCompany)
            : DeterministicFallbackCode(project.OwnerId);

        return new GuaranteePrefillResponse
        {
            ContractorId = $"TRV-CON-{contractorCode}",
            LegalCompanyName = applicantCompany.LegalCompanyName,
            RegistrationNumber = applicantCompany.RegistrationNumber,
            TaxVatNumber = applicantCompany.TaxVatNumber,
            RegisteredAddress = applicantCompany.RegisteredAddress,
            PrimaryContact = FormatContact(applicantCompany.PrimaryContactName, applicantCompany.PositionTitle),
            PrimaryEmail = applicantCompany.PrimaryEmail,
            PrimaryPhone = applicantCompany.PrimaryPhoneNumber,

            ProjectId = project.ProjectCode,
            ProjectName = project.Title,
            Location = project.Location,
            ContractValue = project.ContractValueJod,
            Description = project.Description,
            ContractDuration = project.TimelineText,

            BeneficiaryId = $"TRV-OWN-{beneficiaryCode}",
            BeneficiaryCompanyName = beneficiaryCompany != null
                ? (string.IsNullOrWhiteSpace(beneficiaryCompany.TradingName) ? beneficiaryCompany.LegalCompanyName : beneficiaryCompany.TradingName)
                : (beneficiaryUser?.Name ?? "Unknown"),
            BeneficiaryAddress = beneficiaryCompany?.RegisteredAddress ?? string.Empty,
            BeneficiaryContact = beneficiaryCompany != null
                ? FormatContact(beneficiaryCompany.PrimaryContactName, beneficiaryCompany.PositionTitle)
                : string.Empty,
            BeneficiaryEmail = beneficiaryCompany?.PrimaryEmail ?? beneficiaryUser?.Email ?? string.Empty,
            BeneficiaryPhone = beneficiaryCompany?.PrimaryPhoneNumber ?? beneficiaryUser?.Phone ?? string.Empty,
        };
    }

    private static string FormatContact(string name, string positionTitle) =>
        string.IsNullOrWhiteSpace(positionTitle) ? name : $"{name}, {positionTitle}";

    // Scoped lookup shared by prefill and submit: this contractor must
    // have a bid on this project, and that bid must be Confirmed (i.e.
    // "waiting on the bank to issue the guarantee" — the same state
    // BidService's DefaultActiveNote already describes it as). Anyone
    // else gets the same 404 a nonexistent project would give — never
    // leaks whether a project exists to a contractor who isn't the
    // awarded bidder on it.
    private async Task<(Project project, Bid bid)> ResolveConfirmedBidAsync(Guid contractorId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found.");

        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.ProjectId == project.Id && b.ContractorId == contractorId);
        if (bid == null)
            throw new KeyNotFoundException("Project not found.");

        if (bid.Status != BidStatus.Confirmed)
            throw new InvalidOperationException("A guarantee can only be applied for once your bid on this project has been confirmed.");

        return (project, bid);
    }

    // Blocks re-applying while a decision is still outstanding or already
    // went the contractor's way. Rejected is deliberately NOT blocking —
    // "Apply for New Guarantee" after a rejection is the whole point of
    // that state (see BidStatusMapper). Approved blocks because the bid
    // has already moved to InProgress by then anyway (ResolveConfirmedBidAsync
    // would have rejected it first), but checking here too keeps this
    // guard correct on its own if that ordering ever changes.
    private async Task EnsureNoActiveApplicationAsync(Guid bidId)
    {
        var blockingStatuses = new[] { GuaranteeStatus.PendingBankReview, GuaranteeStatus.Issued, GuaranteeStatus.Approved };
        var hasActive = await _db.GuaranteeApplications
            .AnyAsync(g => g.BidId == bidId && blockingStatuses.Contains(g.Status));

        if (hasActive)
            throw new InvalidOperationException("A guarantee application is already pending or approved for this bid.");
    }

    // Random 4-digit code, checked against CompanyDetails for collisions
    // and retried, persisted once generated — same "generate, verify
    // uniqueness, persist" shape as ProjectService.GenerateUniqueProjectCodeAsync.
    private async Task<string> GetOrCreatePublicCodeAsync(Models.CompanyDetails company)
    {
        if (!string.IsNullOrEmpty(company.PublicCode))
            return company.PublicCode;

        var random = new Random();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = random.Next(1000, 9999).ToString();
            var exists = await _db.CompanyDetails.AnyAsync(c => c.PublicCode == candidate);
            if (!exists)
            {
                company.PublicCode = candidate;
                await _db.SaveChangesAsync();
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique company code. Please try again.");
    }

    // Used only when the beneficiary hasn't submitted Company Details yet
    // (no row to persist a real PublicCode onto). Stable per user so the
    // same placeholder shows up every time, but NOT guaranteed globally
    // unique — purely a display placeholder until that owner completes
    // Company Details and gets a real PublicCode.
    private static string DeterministicFallbackCode(Guid userId)
    {
        var bytes = userId.ToByteArray();
        var value = BitConverter.ToUInt16(bytes, 0) % 9000 + 1000;
        return value.ToString();
    }

    // ── Submit ───────────────────────────────────────────────────────────

    public async Task<SubmitGuaranteeResponse> SubmitAsync(Guid contractorId, SubmitGuaranteeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
            throw new ArgumentException("projectId is required.");

        var (project, bid) = await ResolveConfirmedBidAsync(contractorId, request.ProjectId);
        await EnsureNoActiveApplicationAsync(bid.Id);

        var guaranteeType = ParseGuaranteeType(request.GuaranteeType);
        var guaranteedAmount = ParseAmount(request.GuaranteedAmount);
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "JOD" : request.Currency.Trim().ToUpperInvariant();
        var (validityStart, validityExpiry) = ParseValidityWindow(request.ValidityStart, request.ValidityExpiry);

        if (!ParseFlag(request.ConfirmAccurate) || !ParseFlag(request.AgreeIndemnify) || !ParseFlag(request.AcceptTerms))
            throw new ArgumentException("You must confirm accuracy, agree to indemnify, and accept the terms before submitting.");

        if (string.IsNullOrWhiteSpace(request.SignatureName))
            throw new ArgumentException("A signature name is required.");

        ValidateDocument(request.SignedContract, "signedContract", required: true);
        ValidateDocument(request.LetterOfAward, "letterOfAward", required: true);
        foreach (var doc in request.OtherDocuments ?? new List<IFormFile>())
            ValidateDocument(doc, "otherDocuments", required: true);

        var application = new GuaranteeApplication
        {
            ApplicationCode = await GenerateUniqueApplicationCodeAsync(),
            ContractorId = contractorId,
            ProjectId = project.Id,
            BidId = bid.Id,
            BeneficiaryId = project.OwnerId,

            GuaranteeType = guaranteeType,
            GuaranteedAmount = guaranteedAmount,
            Currency = currency,

            ValidityStart = validityStart,
            ValidityExpiry = validityExpiry,

            SpecialConditions = string.IsNullOrWhiteSpace(request.SpecialConditions) ? null : request.SpecialConditions.Trim(),

            ConfirmAccurate = true,
            AgreeIndemnify = true,
            AcceptTerms = true,
            SignatureName = request.SignatureName.Trim(),

            Status = GuaranteeStatus.PendingBankReview,
        };

        _db.GuaranteeApplications.Add(application);
        await _db.SaveChangesAsync();

        await SaveDocumentAsync(application, request.SignedContract!, GuaranteeDocumentType.SignedContract);
        await SaveDocumentAsync(application, request.LetterOfAward!, GuaranteeDocumentType.LetterOfAward);
        foreach (var doc in request.OtherDocuments ?? new List<IFormFile>())
            await SaveDocumentAsync(application, doc, GuaranteeDocumentType.Other);

        return new SubmitGuaranteeResponse
        {
            GuaranteeApplicationId = application.ApplicationCode,
            Status = application.Status.ToUpperInvariant(),
        };
    }

    private static string ParseGuaranteeType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !GuaranteeTypeFromFrontend.TryGetValue(raw.Trim(), out var mapped))
            throw new ArgumentException("guaranteeType must be one of: performance, bidBond, advancePayment, retention.");

        return mapped;
    }

    private static decimal ParseAmount(string raw)
    {
        if (!decimal.TryParse(raw, out var amount) || amount <= 0)
            throw new ArgumentException("guaranteedAmount must be a positive number.");

        return amount;
    }

    private static (DateTime start, DateTime expiry) ParseValidityWindow(string rawStart, string rawExpiry)
    {
        if (!DateTime.TryParse(rawStart, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start))
            throw new ArgumentException("validityStart must be a valid ISO 8601 date.");

        if (!DateTime.TryParse(rawExpiry, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry))
            throw new ArgumentException("validityExpiry must be a valid ISO 8601 date.");

        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        expiry = DateTime.SpecifyKind(expiry, DateTimeKind.Utc);

        // Enforced server-side per the handoff note — the frontend doesn't
        // validate this today.
        if (expiry <= start)
            throw new ArgumentException("validityExpiry must be after validityStart.");

        return (start, expiry);
    }

    private static bool ParseFlag(string raw) => bool.TryParse(raw, out var value) && value;

    // No file-type restriction — any document format is accepted. Only
    // presence, non-empty, and the 10MB size cap are enforced. If you
    // later want to restrict this back to a specific allow-list (e.g.
    // PDF + images), add the extension check back here — it was removed
    // deliberately, not an oversight.
    private static void ValidateDocument(IFormFile? file, string fieldName, bool required)
    {
        if (file == null)
        {
            if (required) throw new ArgumentException($"{fieldName} is required.");
            return;
        }

        if (file.Length == 0)
            throw new ArgumentException($"{fieldName} is empty.");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException($"{fieldName} exceeds the 10MB limit.");
    }

    private async Task<string> GenerateUniqueApplicationCodeAsync()
    {
        var random = new Random();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"TRV-GT-{random.Next(10000, 99999)}";
            var exists = await _db.GuaranteeApplications.AnyAsync(g => g.ApplicationCode == candidate);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Could not generate a unique guarantee application code. Please try again.");
    }

    private async Task SaveDocumentAsync(GuaranteeApplication application, IFormFile file, string documentType)
    {
        var basePath = Path.IsPathRooted(_storageOptions.GuaranteeDocumentsPath)
            ? _storageOptions.GuaranteeDocumentsPath
            : Path.Combine(_contentRootPath, _storageOptions.GuaranteeDocumentsPath);

        var applicationFolder = Path.Combine(basePath, application.ApplicationCode);
        Directory.CreateDirectory(applicationFolder);

        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(applicationFolder, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        _db.GuaranteeDocuments.Add(new GuaranteeDocument
        {
            GuaranteeApplicationId = application.Id,
            DocumentType = documentType,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
        });

        await _db.SaveChangesAsync();
    }

    // ── Owner-side read ──────────────────────────────────────────────────

    public async Task<OwnerGuaranteeDto?> GetOwnerGuaranteeAsync(Guid ownerId, string projectId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null) return null;

        var application = await _db.GuaranteeApplications
            .Where(g => g.ProjectId == project.Id)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        if (application == null) return null;

        return await BuildOwnerGuaranteeDtoAsync(application, project);
    }

    // ── Owner decision ───────────────────────────────────────────────────
    // The owner's confirm/reject only applies once the bank has already
    // issued the guarantee (Status == Issued) — this is the second half of
    // the two-stage flow, after GuaranteesController/BankController's
    // bank-side Issue/Reject. Scoped to ownerId so only that project's
    // beneficiary can decide. Keyed by projectId, matching the access
    // pattern GetOwnerGuaranteeAsync already uses — the owner's screen
    // only ever knows which project it's looking at, not the application's
    // code.

    public async Task<OwnerGuaranteeDto> ConfirmAsync(Guid ownerId, string projectId)
    {
        var (application, project) = await GetOwnerApplicationInStatusOrThrowAsync(ownerId, projectId, GuaranteeStatus.Issued);

        application.Status = GuaranteeStatus.Approved;
        application.UpdatedAt = DateTime.UtcNow;

        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.Id == application.BidId);
        if (bid == null)
            throw new KeyNotFoundException("Bid not found for this guarantee application.");

        // Confirmed -> InProgress: the transition nothing else in this
        // codebase makes yet (see the comment on BidStatus.InProgress).
        bid.Status = BidStatus.InProgress;
        bid.Note = null; // derives "Work in progress" at read time
        bid.UpdatedAt = DateTime.UtcNow;

        // Project.Status previously never left Awarded on approval — the
        // bid moved to InProgress but nothing flipped the project, so My
        // Projects/Project Detail kept showing it stuck under "Awarded"
        // forever. This is the missing transition.
        project.Status = ProjectStatus.InProgress;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildOwnerGuaranteeDtoAsync(application, project);
    }

    public async Task<OwnerGuaranteeDto> RejectByOwnerAsync(Guid ownerId, string projectId, string? reason)
    {
        var (application, project) = await GetOwnerApplicationInStatusOrThrowAsync(ownerId, projectId, GuaranteeStatus.Issued);

        application.Status = GuaranteeStatus.Rejected;
        application.RejectedBy = GuaranteeRejectedBy.Owner;
        application.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        application.UpdatedAt = DateTime.UtcNow;

        // Bid.Status stays Confirmed on purpose — "Guarantee Rejected" is
        // a display state layered on top via BidStatusMapper, not a
        // separate BidStatus, because the contractor's still mid-flow on
        // this same confirmed bid (Back Off or apply again). Only the
        // Project flips here, so BackOffBidAsync's existing
        // GuaranteeRejectedByYou check produces the right note if/when
        // the contractor does back off.
        project.Status = ProjectStatus.GuaranteeRejectedByYou;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildOwnerGuaranteeDtoAsync(application, project);
    }

    // Loads the latest application on a project and enforces both that the
    // caller is the project's beneficiary (owner) and that the application
    // is actually in the state this decision expects. A non-owner (or a
    // nonexistent project) gets the same 404 either way — never leaks
    // whether a project exists to someone who isn't its beneficiary, same
    // pattern as ResolveConfirmedBidAsync on the contractor side.
    private async Task<(GuaranteeApplication application, Project project)> GetOwnerApplicationInStatusOrThrowAsync(
        Guid ownerId, string projectId, string requiredStatus)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.ProjectCode == projectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found.");

        var application = await _db.GuaranteeApplications
            .Where(g => g.ProjectId == project.Id)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        if (application == null)
            throw new KeyNotFoundException("No guarantee application found for this project.");

        if (application.Status != requiredStatus)
            throw new InvalidOperationException("This guarantee application isn't awaiting your decision.");

        return (application, project);
    }

    // Maps internal Status/GuaranteeType storage onto the external
    // vocabulary guarantee_review_model.dart expects. Kept next to
    // GuaranteeTypeFromFrontend at the top of this class conceptually,
    // just the reverse direction (internal -> display) rather than
    // parse (frontend -> internal).
    private static readonly Dictionary<string, string> GuaranteeTypeDisplayLabel = new()
    {
        [GuaranteeTypes.Performance] = "Performance Guarantee",
        [GuaranteeTypes.BidBond] = "Bid Bond Guarantee",
        [GuaranteeTypes.AdvancePayment] = "Advance Payment Guarantee",
        [GuaranteeTypes.Retention] = "Retention Guarantee",
    };

    private static string ExternalGuaranteeStatus(string status) => status switch
    {
        GuaranteeStatus.PendingBankReview => "PENDING_REVIEW",
        GuaranteeStatus.Issued => "ISSUED", // bank issued it; awaiting your confirmation
        GuaranteeStatus.Approved => "ACTIVE",
        GuaranteeStatus.Rejected => "REJECTED",
        _ => status.ToUpperInvariant(),
    };

    private async Task<OwnerGuaranteeDto> BuildOwnerGuaranteeDtoAsync(GuaranteeApplication application, Project project)
    {
        var contractorCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == application.ContractorId);
        var contractorName = contractorCompany != null
            ? (string.IsNullOrWhiteSpace(contractorCompany.TradingName) ? contractorCompany.LegalCompanyName : contractorCompany.TradingName)
            : (await _db.Users.FirstOrDefaultAsync(u => u.Id == application.ContractorId))?.Name ?? "Unknown";

        var beneficiaryCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == application.BeneficiaryId);
        var beneficiaryName = beneficiaryCompany != null
            ? (string.IsNullOrWhiteSpace(beneficiaryCompany.TradingName) ? beneficiaryCompany.LegalCompanyName : beneficiaryCompany.TradingName)
            : (await _db.Users.FirstOrDefaultAsync(u => u.Id == application.BeneficiaryId))?.Name ?? "You";

        // The contractor's connected bank issues the guarantee — same
        // BankConnection row CapabilityScore/BankConnectionController
        // already read from. Falls back gracefully if the contractor
        // somehow submitted without one connected (shouldn't happen in
        // practice, but never worth a 500 over a display field).
        var bankConnection = await _db.BankConnections.FirstOrDefaultAsync(b => b.UserId == application.ContractorId);

        return new OwnerGuaranteeDto
        {
            GuaranteeId = application.ApplicationCode,
            ProjectId = project.ProjectCode,
            ProjectTitle = project.Title,
            ContractorName = contractorName,
            AwardedBidder = new AwardedBidderDto
            {
                BidId = application.BidId.ToString(),
                CompanyName = contractorName,
                Classification = contractorCompany?.ClassificationCode ?? string.Empty,
                Eligible = true
            },
            Beneficiary = $"{beneficiaryName} (You)",
            IssuingBank = bankConnection?.BankName ?? "Bank not yet connected",
            AmountJod = application.GuaranteedAmount,
            Type = GuaranteeTypeDisplayLabel.GetValueOrDefault(application.GuaranteeType, application.GuaranteeType),
            Status = ExternalGuaranteeStatus(application.Status),
            IssueDate = application.IssuedAt?.ToString("yyyy-MM-dd"),
            ValidUntil = application.ValidityExpiry.ToString("yyyy-MM-dd"),
            ClaimDate = null, // claim lifecycle isn't modelled yet
            RejectionReason = application.RejectionReason,
        };
    }

    // ── Bank-facing ──────────────────────────────────────────────────────
    // One bank account sees every application — no per-bank scoping (see
    // the comment on IGuaranteeService).

    public async Task<List<BankGuaranteeDto>> GetBankRequestsAsync()
    {
        var applications = await _db.GuaranteeApplications
            .Where(g => g.Status == GuaranteeStatus.PendingBankReview)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();

        var result = new List<BankGuaranteeDto>();
        foreach (var application in applications)
            result.Add(await BuildBankGuaranteeDtoAsync(application));
        return result;
    }

    public async Task<List<BankGuaranteeDto>> GetBankGuaranteesAsync()
    {
        var activeStatuses = new[] { GuaranteeStatus.Issued, GuaranteeStatus.Approved };
        var applications = await _db.GuaranteeApplications
            .Where(g => activeStatuses.Contains(g.Status))
            .OrderByDescending(g => g.IssuedAt)
            .ToListAsync();

        var result = new List<BankGuaranteeDto>();
        foreach (var application in applications)
            result.Add(await BuildBankGuaranteeDtoAsync(application));
        return result;
    }

    public async Task<BankGuaranteeDto> IssueAsync(string applicationCode)
    {
        var application = await GetPendingBankApplicationOrThrowAsync(applicationCode);

        application.Status = GuaranteeStatus.Issued;
        application.IssuedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildBankGuaranteeDtoAsync(application);
    }

    public async Task<BankGuaranteeDto> RejectByBankAsync(string applicationCode, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.");

        var application = await GetPendingBankApplicationOrThrowAsync(applicationCode);

        application.Status = GuaranteeStatus.Rejected;
        application.RejectedBy = GuaranteeRejectedBy.Bank;
        application.RejectionReason = reason.Trim();
        application.UpdatedAt = DateTime.UtcNow;

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == application.ProjectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found for this guarantee application.");

        // Same terminal project state whichever side rejects — see the
        // comment on ProjectStatus.GuaranteeRejectedByYou; it describes
        // the project's guarantee having died, not who killed it.
        project.Status = ProjectStatus.GuaranteeRejectedByYou;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildBankGuaranteeDtoAsync(application);
    }

    private async Task<GuaranteeApplication> GetPendingBankApplicationOrThrowAsync(string applicationCode)
    {
        var application = await _db.GuaranteeApplications
            .FirstOrDefaultAsync(g => g.ApplicationCode == applicationCode);

        if (application == null)
            throw new KeyNotFoundException("Guarantee application not found.");

        if (application.Status != GuaranteeStatus.PendingBankReview)
            throw new InvalidOperationException("This application has already been decided.");

        return application;
    }

    private static string ExternalBankGuaranteeStatus(string status) => status switch
    {
        GuaranteeStatus.PendingBankReview => "PENDING_REVIEW",
        GuaranteeStatus.Issued => "ISSUED",
        GuaranteeStatus.Approved => "CONFIRMED", // owner has confirmed it, on top of the bank's issue
        GuaranteeStatus.Rejected => "REJECTED",
        _ => status.ToUpperInvariant(),
    };

    private static readonly Dictionary<string, string> BankDocumentDisplayName = new()
    {
        [GuaranteeDocumentType.SignedContract] = "Signed Contract / Agreement",
        [GuaranteeDocumentType.LetterOfAward] = "Letter of Award",
        [GuaranteeDocumentType.Other] = "Other Supporting Documents",
    };

    private static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N0", CultureInfo.InvariantCulture)}";

    private static string FormatDate(DateTime date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string FormatValidity(DateTime start, DateTime expiry) =>
        $"{FormatDate(start)} \u2013 {FormatDate(expiry)}";

    private async Task<BankGuaranteeDto> BuildBankGuaranteeDtoAsync(GuaranteeApplication application)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == application.ProjectId);
        if (project == null)
            throw new KeyNotFoundException("Project not found for this guarantee application.");

        var contractorCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == application.ContractorId);
        var contractorUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == application.ContractorId);
        var contractorCode = contractorCompany != null
            ? await GetOrCreatePublicCodeAsync(contractorCompany)
            : DeterministicFallbackCode(application.ContractorId);
        var contractorName = contractorCompany != null
            ? (string.IsNullOrWhiteSpace(contractorCompany.TradingName) ? contractorCompany.LegalCompanyName : contractorCompany.TradingName)
            : (contractorUser?.Name ?? "Unknown");

        var beneficiaryCompany = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == application.BeneficiaryId);
        var beneficiaryUser = beneficiaryCompany == null
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == application.BeneficiaryId)
            : null;
        var beneficiaryName = beneficiaryCompany != null
            ? (string.IsNullOrWhiteSpace(beneficiaryCompany.TradingName) ? beneficiaryCompany.LegalCompanyName : beneficiaryCompany.TradingName)
            : (beneficiaryUser?.Name ?? "Unknown");

        var documents = await _db.GuaranteeDocuments
            .Where(d => d.GuaranteeApplicationId == application.Id)
            .ToListAsync();

        var documentDtos = new List<BankDocumentDto>();
        foreach (var docType in new[] { GuaranteeDocumentType.SignedContract, GuaranteeDocumentType.LetterOfAward, GuaranteeDocumentType.Other })
        {
            documentDtos.Add(new BankDocumentDto
            {
                Name = BankDocumentDisplayName[docType],
                Status = documents.Any(d => d.DocumentType == docType) ? "Uploaded" : "None",
            });
        }

        return new BankGuaranteeDto
        {
            Id = application.ApplicationCode,
            Contractor = contractorName,
            RequestedDate = FormatDate(application.CreatedAt),
            IssuedDate = application.IssuedAt.HasValue ? FormatDate(application.IssuedAt.Value) : null,
            Status = ExternalBankGuaranteeStatus(application.Status),
            RejectionReason = application.RejectionReason,
            Applicant = new BankApplicantDto
            {
                ContractorId = $"TRV-CON-{contractorCode}",
                LegalName = contractorCompany?.LegalCompanyName ?? contractorName,
                Cr = contractorCompany?.RegistrationNumber ?? string.Empty,
                Tax = contractorCompany?.TaxVatNumber ?? string.Empty,
                Address = contractorCompany?.RegisteredAddress ?? string.Empty,
                Contact = contractorCompany != null
                    ? FormatContact(contractorCompany.PrimaryContactName, contractorCompany.PositionTitle)
                    : string.Empty,
                Email = contractorCompany?.PrimaryEmail ?? contractorUser?.Email ?? string.Empty,
                Phone = contractorCompany?.PrimaryPhoneNumber ?? contractorUser?.Phone ?? string.Empty,
            },
            Project = new BankProjectDto
            {
                Name = project.Title,
                Location = project.Location,
                Value = FormatMoney(project.ContractValueJod, project.Currency),
                Description = project.Description,
                Duration = project.TimelineText,
            },
            Guarantee = new BankGuaranteeDetailsDto
            {
                Type = GuaranteeTypeDisplayLabel.GetValueOrDefault(application.GuaranteeType, application.GuaranteeType),
                Amount = FormatMoney(application.GuaranteedAmount, application.Currency),
                Validity = FormatValidity(application.ValidityStart, application.ValidityExpiry),
                ExpiryDate = application.ValidityExpiry.ToString("yyyy-MM-dd"),
                Conditions = string.IsNullOrWhiteSpace(application.SpecialConditions) ? "\u2014" : application.SpecialConditions,
            },
            Beneficiary = new BankBeneficiaryDto
            {
                Company = beneficiaryName,
                Address = beneficiaryCompany?.RegisteredAddress ?? string.Empty,
                Contact = beneficiaryCompany != null
                    ? FormatContact(beneficiaryCompany.PrimaryContactName, beneficiaryCompany.PositionTitle)
                    : string.Empty,
                Email = beneficiaryCompany?.PrimaryEmail ?? beneficiaryUser?.Email ?? string.Empty,
                Phone = beneficiaryCompany?.PrimaryPhoneNumber ?? beneficiaryUser?.Phone ?? string.Empty,
            },
            Documents = documentDtos,
            Declarations = new BankDeclarationsDto
            {
                Accuracy = application.ConfirmAccurate,
                Indemnify = application.AgreeIndemnify,
                Terms = application.AcceptTerms,
                Signature = application.SignatureName,
            },
        };
    }
}
