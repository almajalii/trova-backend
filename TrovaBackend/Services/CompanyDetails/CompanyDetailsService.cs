using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrovaBackend.Data;
using TrovaBackend.DTOs;

namespace TrovaBackend.Services
{
    public interface ICompanyDetailsService
    {
        Task<ScoreClassificationDto> SubmitCompanyDetailsAsync(string userId, CompanyDetailsDraftDto draft);
        Task<CompanyDetailsRecordDto?> GetCompanyDetailsAsync(string userId);
    }

    public class CompanyDetailsService : ICompanyDetailsService
    {
        private readonly AppDbContext _db;
        private readonly TrovaBackend.Services.CompanyDetails.CompanyClassificationOptions _options;

        public CompanyDetailsService(AppDbContext db, IOptions<TrovaBackend.Services.CompanyDetails.CompanyClassificationOptions> options)
        {
            _db = db;
            _options = options.Value;
        }

        // Upsert against the real CompanyDetails table — one row per user,
        // same pattern as BankConnection/CapabilityScore (HasIndex(UserId).IsUnique()).
        public async Task<ScoreClassificationDto> SubmitCompanyDetailsAsync(string userId, CompanyDetailsDraftDto draft)
        {
            var userGuid = Guid.Parse(userId);

            var entity = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userGuid);
            var isNew = entity == null;
            entity ??= new Models.CompanyDetails { UserId = userGuid };

            entity.LegalCompanyName = draft.LegalCompanyName;
            entity.TradingName = draft.TradingName;
            entity.RegistrationNumber = draft.RegistrationNumber;
            entity.TaxVatNumber = draft.TaxVatNumber;
            entity.LegalStructure = draft.LegalStructure;
            entity.YearOfEstablishment = draft.YearOfEstablishment;
            entity.RegisteredAddress = draft.RegisteredAddress;
            entity.CountryOfRegistration = draft.CountryOfRegistration;
            entity.PrimaryContactName = draft.PrimaryContactName;
            entity.PositionTitle = draft.PositionTitle;
            entity.PrimaryEmail = draft.PrimaryEmail;
            entity.PrimaryPhoneNumber = draft.PrimaryPhoneNumber;
            entity.BusinessLicenseNumber = draft.BusinessLicenseNumber;
            entity.ContractorClassificationGrade = draft.ContractorClassificationGrade;
            entity.Sectors = draft.Sectors;
            entity.YearsOfExperience = draft.YearsOfExperience;
            entity.TeamSize = draft.TeamSize;
            entity.AnnualRevenueJod = draft.AnnualRevenueJod;
            entity.PrimaryBankName = draft.PrimaryBankName;
            entity.IbanNumber = draft.IbanNumber;
            entity.SwiftBicCode = draft.SwiftBicCode;
            entity.BankBranchNameCity = draft.BankBranchNameCity;
            entity.UpdatedAt = DateTime.UtcNow;

            // Classification still keys off years *in operation*
            // (YearOfEstablishment-derived), not the self-reported
            // YearsOfExperience — same "don't trust a client-submitted
            // number for a scoring input" principle used for Bid
            // eligibility elsewhere. YearsOfExperience itself is now stored
            // and read back as-submitted (it's informational, shown to
            // admins/other users, not fed into the formula).
            var yearsInOperation = DateTime.UtcNow.Year - draft.YearOfEstablishment;
            var classification = CalculateClassification(draft.TeamSize, draft.AnnualRevenueJod, yearsInOperation);

            entity.ClassificationCode = classification.Code;
            entity.ClassificationLabel = classification.Label;

            if (isNew)
                _db.CompanyDetails.Add(entity);

            await _db.SaveChangesAsync();

            return classification;
        }

        public async Task<CompanyDetailsRecordDto?> GetCompanyDetailsAsync(string userId)
        {
            var userGuid = Guid.Parse(userId);
            var entity = await _db.CompanyDetails.FirstOrDefaultAsync(c => c.UserId == userGuid);
            if (entity == null) return null;

            return new CompanyDetailsRecordDto
            {
                LegalCompanyName = entity.LegalCompanyName,
                TradingName = entity.TradingName,
                RegistrationNumber = entity.RegistrationNumber,
                TaxVatNumber = entity.TaxVatNumber,
                LegalStructure = entity.LegalStructure,
                YearOfEstablishment = entity.YearOfEstablishment,
                RegisteredAddress = entity.RegisteredAddress,
                CountryOfRegistration = entity.CountryOfRegistration,
                PrimaryContactName = entity.PrimaryContactName,
                PositionTitle = entity.PositionTitle,
                PrimaryEmail = entity.PrimaryEmail,
                PrimaryPhoneNumber = entity.PrimaryPhoneNumber,
                BusinessLicenseNumber = entity.BusinessLicenseNumber,
                ContractorClassificationGrade = entity.ContractorClassificationGrade,
                Sectors = entity.Sectors,
                YearsOfExperience = entity.YearsOfExperience,
                TeamSize = entity.TeamSize,
                AnnualRevenueJod = entity.AnnualRevenueJod,
                PrimaryBankName = entity.PrimaryBankName,
                IbanNumber = entity.IbanNumber,
                SwiftBicCode = entity.SwiftBicCode,
                BankBranchNameCity = entity.BankBranchNameCity,
                Classification = new ScoreClassificationDto
                {
                    Code = entity.ClassificationCode,
                    Label = entity.ClassificationLabel
                }
            };
        }

        // Majority-of-3 rule against configured (appsettings.json-driven)
        // thresholds — two tiers checked independently rather than the old
        // single-threshold-set approach, since Class B has its own bar.
        private ScoreClassificationDto CalculateClassification(int teamSize, decimal revenue, int yearsInOperation)
        {
            var classAPoints = 0;
            if (teamSize >= _options.ClassA.MinTeamSize) classAPoints++;
            if (revenue >= _options.ClassA.MinRevenueJod) classAPoints++;
            if (yearsInOperation >= _options.ClassA.MinYearsInOperation) classAPoints++;
            if (classAPoints >= 2)
                return new ScoreClassificationDto { Code = "A", Label = "Class A" };

            var classBPoints = 0;
            if (teamSize >= _options.ClassB.MinTeamSize) classBPoints++;
            if (revenue >= _options.ClassB.MinRevenueJod) classBPoints++;
            if (yearsInOperation >= _options.ClassB.MinYearsInOperation) classBPoints++;
            if (classBPoints >= 2)
                return new ScoreClassificationDto { Code = "B", Label = "Class B" };

            return new ScoreClassificationDto { Code = "C", Label = "Class C" };
        }
    }
}