using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrovaBackend.Data;
using TrovaBackend.DTOs.CompanyDetails;
using TrovaBackend.Models;

namespace TrovaBackend.Services.CompanyDetails;

public interface ICompanyDetailsService
{
    Task<CompanyDetailsResponse> SubmitAsync(Guid userId, SubmitCompanyDetailsRequest request);
    Task<CompanyDetailsFullResponse> GetAsync(Guid userId);
}

public class CompanyDetailsService : ICompanyDetailsService
{
    private readonly AppDbContext _db;
    private readonly CompanyClassificationOptions _classificationOptions;

    public CompanyDetailsService(AppDbContext db, IOptions<CompanyClassificationOptions> classificationOptions)
    {
        _db = db;
        _classificationOptions = classificationOptions.Value;
    }

    public async Task<CompanyDetailsResponse> SubmitAsync(Guid userId, SubmitCompanyDetailsRequest request)
    {
        ValidateSectors(request.Sectors);

        var entity = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userId);
        var isNew = entity == null;
        entity ??= new Models.CompanyDetails { UserId = userId };

        entity.CompanyName = request.CompanyName.Trim();
        entity.Sectors = request.Sectors.Distinct().ToList();
        entity.RegistrationNumber = request.RegistrationNumber.Trim();
        entity.YearsInOperation = request.YearsInOperation;
        entity.TeamSize = request.TeamSize;
        entity.AnnualRevenueJod = request.AnnualRevenueJod;

        var (code, label) = ClassifyCompany(request.TeamSize, request.AnnualRevenueJod, request.YearsInOperation);
        entity.ClassificationCode = code;
        entity.ClassificationLabel = label;
        entity.UpdatedAt = DateTime.UtcNow;

        if (isNew)
            _db.CompanyDetails.Add(entity);

        await _db.SaveChangesAsync();

        return new CompanyDetailsResponse
        {
            Classification = new ClassificationDto { Code = code, Label = label }
        };
    }

    public async Task<CompanyDetailsFullResponse> GetAsync(Guid userId)
    {
        var entity = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new KeyNotFoundException("Company details have not been submitted yet.");

        return new CompanyDetailsFullResponse
        {
            CompanyName = entity.CompanyName,
            Sectors = entity.Sectors,
            RegistrationNumber = entity.RegistrationNumber,
            YearsInOperation = entity.YearsInOperation,
            TeamSize = entity.TeamSize,
            AnnualRevenueJod = entity.AnnualRevenueJod,
            Classification = new ClassificationDto
            {
                Code = entity.ClassificationCode,
                Label = entity.ClassificationLabel
            }
        };
    }

    private static void ValidateSectors(List<string> sectors)
    {
        var invalid = sectors.Where(s => !TrovaSectors.All.Contains(s)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException(
                $"Invalid sector(s): {string.Join(", ", invalid)}. Allowed values: {string.Join(", ", TrovaSectors.All)}");
    }

    // ── Classification: majority-of-3 signals ───────────────────────────
    // A company qualifies for a tier if it meets at least 2 of that tier's
    // 3 criteria (team size, revenue, years in operation) — a single
    // strong signal (e.g. huge revenue but brand new, 1-person company)
    // isn't enough on its own. Thresholds come from appsettings.json
    // ("CompanyClassification" section), not hardcoded here.
    private (string Code, string Label) ClassifyCompany(int teamSize, decimal annualRevenueJod, int yearsInOperation)
    {
        if (MeetsAtLeastTwo(teamSize, annualRevenueJod, yearsInOperation, _classificationOptions.ClassA))
            return ("A", "Large Enterprise");

        if (MeetsAtLeastTwo(teamSize, annualRevenueJod, yearsInOperation, _classificationOptions.ClassB))
            return ("B", "Medium Enterprise");

        return ("C", "Small Enterprise");
    }

    private static bool MeetsAtLeastTwo(int teamSize, decimal annualRevenueJod, int yearsInOperation, ClassTierThresholds t)
    {
        var signalsMet = 0;
        if (teamSize >= t.MinTeamSize) signalsMet++;
        if (annualRevenueJod >= t.MinRevenueJod) signalsMet++;
        if (yearsInOperation >= t.MinYearsInOperation) signalsMet++;
        return signalsMet >= 2;
    }
}