using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrovaBackend.Data;
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

        var guaranteeType = ParseGuaranteeType(request.GuaranteeType);
        var guaranteedAmount = ParseAmount(request.GuaranteedAmount);
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "JOD" : request.Currency.Trim().ToUpperInvariant();
        var (validityStart, validityExpiry) = ParseValidityWindow(request.ValidityStart, request.ValidityExpiry);

        if (!ParseFlag(request.ConfirmAccurate) || !ParseFlag(request.AgreeIndemnify) || !ParseFlag(request.AcceptTerms))
            throw new ArgumentException("You must confirm accuracy, agree to indemnify, and accept the terms before submitting.");

        if (string.IsNullOrWhiteSpace(request.SignatureName))
            throw new ArgumentException("A signature name is required.");

        ValidatePdf(request.SignedContract, "signedContract", required: true);
        ValidatePdf(request.LetterOfAward, "letterOfAward", required: true);
        foreach (var doc in request.OtherDocuments ?? new List<IFormFile>())
            ValidatePdf(doc, "otherDocuments", required: true);

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

    private static void ValidatePdf(IFormFile? file, string fieldName, bool required)
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

        // Extension is checked rather than trusting IFormFile.ContentType —
        // some mobile multipart clients send generic/absent content types.
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{fieldName} must be a PDF file.");
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
}
