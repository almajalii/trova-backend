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
        // Mocking the database behavior
        private static readonly Dictionary<string, CompanyDetailsRecordDto> _mockDb = new();

        public async Task<ScoreClassificationDto> SubmitCompanyDetailsAsync(string userId, CompanyDetailsDraftDto draft)
        {
            // Calculate classification based on majority-of-3 rule (Mock logic)
            var classification = CalculateClassificationFit(draft.TeamSize, draft.AnnualRevenueJod, draft.YearsOfExperience);

            var record = new CompanyDetailsRecordDto
            {
                LegalCompanyName = draft.LegalCompanyName,
                TradingName = draft.TradingName,
                RegistrationNumber = draft.RegistrationNumber,
                TaxVatNumber = draft.TaxVatNumber,
                LegalStructure = draft.LegalStructure,
                YearOfEstablishment = draft.YearOfEstablishment,
                RegisteredAddress = draft.RegisteredAddress,
                CountryOfRegistration = draft.CountryOfRegistration,
                PrimaryContactName = draft.PrimaryContactName,
                PositionTitle = draft.PositionTitle,
                PrimaryEmail = draft.PrimaryEmail,
                PrimaryPhoneNumber = draft.PrimaryPhoneNumber,
                BusinessLicenseNumber = draft.BusinessLicenseNumber,
                ContractorClassificationGrade = draft.ContractorClassificationGrade,
                Sectors = draft.Sectors,
                YearsOfExperience = draft.YearsOfExperience,
                TeamSize = draft.TeamSize,
                AnnualRevenueJod = draft.AnnualRevenueJod,
                PrimaryBankName = draft.PrimaryBankName,
                IbanNumber = draft.IbanNumber,
                SwiftBicCode = draft.SwiftBicCode,
                BankBranchNameCity = draft.BankBranchNameCity,
                Classification = classification
            };

            // Save to DB (mocked)
            _mockDb[userId] = record;

            return await Task.FromResult(classification);
        }

        public async Task<CompanyDetailsRecordDto?> GetCompanyDetailsAsync(string userId)
        {
            _mockDb.TryGetValue(userId, out var record);
            return await Task.FromResult(record);
        }

        private ScoreClassificationDto CalculateClassificationFit(int teamSize, decimal revenue, int years)
        {
            // Placeholder for your actual majority-of-3 logic
            int points = 0;
            if (teamSize >= 50) points++;
            if (revenue >= 500000) points++;
            if (years >= 10) points++;

            if (points >= 2) return new ScoreClassificationDto { Code = "A", Label = "Large Enterprise" };
            if (points == 1) return new ScoreClassificationDto { Code = "B", Label = "Medium Enterprise" };
            return new ScoreClassificationDto { Code = "C", Label = "Small Enterprise" };
        }
    }
}