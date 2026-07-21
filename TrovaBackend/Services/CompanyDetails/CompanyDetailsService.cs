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
        //
        // NOTE: CompanyDetailsDraftDto still carries YearsOfExperience,
        // PrimaryBankName, IbanNumber, SwiftBicCode, BankBranchNameCity —
        // fields the real Models.CompanyDetails entity doesn't have columns
        // for (bank data lives in the separate, real BankConnection table
        // via the JOFS flow instead). Those fields are accepted from the
        // client but not persisted here. Flagging rather than silently
        // dropping: if the frontend actually depends on reading them back,
        // that needs a migration + a decision on where they belong.
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
            entity.TeamSize = draft.TeamSize;
            entity.AnnualRevenueJod = draft.AnnualRevenueJod;
            entity.UpdatedAt = DateTime.UtcNow;

            // Years in operation derived from YearOfEstablishment rather than
            // trusting the client-submitted YearsOfExperience — same
            // "don't trust a client-supplied flag" principle used for Bid
            // eligibility elsewhere.
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
                // Derived, not stored — see NOTE above.
                YearsOfExperience = DateTime.UtcNow.Year - entity.YearOfEstablishment,
                TeamSize = entity.TeamSize,
                AnnualRevenueJod = entity.AnnualRevenueJod,
                // Not modelled on Models.CompanyDetails — bank data belongs
                // to the real BankConnection table. Left blank rather than
                // fabricated.
                PrimaryBankName = string.Empty,
                IbanNumber = string.Empty,
                SwiftBicCode = string.Empty,
                BankBranchNameCity = string.Empty,
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