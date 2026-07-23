using System.Text.Json.Serialization;

namespace TrovaBackend.Services.BankConnection;

// Mirrors the "transactions" schema from JoPACC's Transactions v0.4.3 RAML
// spec — GET /accounts/{accountId}/transactions. Only fields Trova
// actually reads for the cashflow calculation are typed out.

public class JofsTransactionsListResponse
{
    [JsonPropertyName("data")]
    public List<JofsTransaction> Data { get; set; } = new();
}

public class JofsTransaction
{
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("transactionAmount")]
    public JofsAmount? TransactionAmount { get; set; }

    // "credit" or "debit" — direction relative to the inquired account.
    [JsonPropertyName("transactionType")]
    public string TransactionType { get; set; } = string.Empty;

    [JsonPropertyName("transactionStatus")]
    public string TransactionStatus { get; set; } = string.Empty;

    [JsonPropertyName("settlementDateTime")]
    public DateTimeOffset? SettlementDateTime { get; set; }
}

public class JofsAmount
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "JOD";
}
