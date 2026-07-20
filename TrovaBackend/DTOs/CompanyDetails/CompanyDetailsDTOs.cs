using System.ComponentModel.DataAnnotations;

namespace TrovaBackend.DTOs
{
    // Standard response wrapper to match your { "data": { ... } } Dart expectation
    public class ApiResponse<T>
    {
        public required T Data { get; set; }
    }

    public class ScoreClassificationDto
    {
        public required string Code { get; set; }
        public required string Label { get; set; }
    }

    public class CompanyDetailsDraftDto
    {
        [Required] public string LegalCompanyName { get; set; } = string.Empty;
        public string TradingName { get; set; } = string.Empty;
        [Required] public string RegistrationNumber { get; set; } = string.Empty;
        [Required] public string TaxVatNumber { get; set; } = string.Empty;
        [Required] public string LegalStructure { get; set; } = string.Empty;
        public int YearOfEstablishment { get; set; }
        [Required] public string RegisteredAddress { get; set; } = string.Empty;
        [Required] public string CountryOfRegistration { get; set; } = string.Empty;

        [Required] public string PrimaryContactName { get; set; } = string.Empty;
        [Required] public string PositionTitle { get; set; } = string.Empty;
        [Required, EmailAddress] public string PrimaryEmail { get; set; } = string.Empty;
        [Required] public string PrimaryPhoneNumber { get; set; } = string.Empty;

        [Required] public string BusinessLicenseNumber { get; set; } = string.Empty;
        [Required] public string ContractorClassificationGrade { get; set; } = string.Empty;

        // Custom validation can be added here to restrict to kAllowedSectors
        [Required, MinLength(1)] public List<string> Sectors { get; set; } = new();

        public int YearsOfExperience { get; set; }
        public int TeamSize { get; set; }
        public decimal AnnualRevenueJod { get; set; }

        [Required] public string PrimaryBankName { get; set; } = string.Empty;
        [Required] public string IbanNumber { get; set; } = string.Empty;
        [Required] public string SwiftBicCode { get; set; } = string.Empty;
        [Required] public string BankBranchNameCity { get; set; } = string.Empty;
    }

    public class CompanyDetailsRecordDto : CompanyDetailsDraftDto
    {
        public required ScoreClassificationDto Classification { get; set; }
    }
}