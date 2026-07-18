using System.ComponentModel.DataAnnotations;

namespace TrovaBackend.DTOs.BankConnection;

public static class TrovaBanks
{
    public const string ArabBank = "arab_bank";
    public const string HousingBank = "housing_bank";
    public const string BankOfJordan = "bank_of_jordan";
    public const string BankAlEtihad = "bank_al_etihad";
    public const string CairoAmmanBank = "cairo_amman_bank";
    public const string JordanKuwaitBank = "jordan_kuwait_bank";
    public const string JordanAhliBank = "jordan_ahli_bank";
    public const string CapitalBank = "capital_bank";
    public const string Ajib = "ajib";
    public const string InvestBank = "investbank";
    public const string JordanCommercialBank = "jordan_commercial_bank";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [ArabBank] = "Arab Bank",
        [HousingBank] = "Housing Bank",
        [BankOfJordan] = "Bank of Jordan",
        [BankAlEtihad] = "Bank al Etihad",
        [CairoAmmanBank] = "Cairo Amman Bank",
        [JordanKuwaitBank] = "Jordan Kuwait Bank",
        [JordanAhliBank] = "Jordan Ahli Bank",
        [CapitalBank] = "Capital Bank of Jordan",
        [Ajib] = "Arab Jordan Investment Bank",
        [InvestBank] = "INVESTBANK",
        [JordanCommercialBank] = "Jordan Commercial Bank"
    };
}

// NOTE: this is my proposed contract, not confirmed against an existing
// Flutter bank_connection_service.dart — I didn't have that file to check
// field names against. Matches the Connect Bank screen's bank list
// (Arab Bank / Housing Bank / Cairo Amman Bank) and Bank Connected screen's
// need to display the bank name. Flag any mismatch with the actual Flutter
// service and I'll adjust.
//
// RemainingDebtCapacityJod and NumberOfDelinquentDebts are self-reported —
// captured here rather than via JOFS, since no JOFS service (Accounts,
// Balances, or Loans) exposes either concept. See BankConnection.cs for
// the full provenance breakdown.
public class ConnectBankRequest
{
    [Required(ErrorMessage = "Bank code is required")]
    public string BankCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Remaining debt capacity is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Remaining debt capacity cannot be negative")]
    public decimal RemainingDebtCapacityJod { get; set; }

    [Required(ErrorMessage = "Number of delinquent debts is required")]
    [Range(0, 1000, ErrorMessage = "Number of delinquent debts must be realistic")]
    public int NumberOfDelinquentDebts { get; set; }
}

public class ConnectBankResponse
{
    public string BankName { get; set; } = string.Empty;
}
