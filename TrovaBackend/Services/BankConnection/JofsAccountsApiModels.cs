using System.Text.Json.Serialization;

namespace TrovaBackend.Services.BankConnection;

// Mirrors the "account" schema from JoPACC's Accounts v0.4.3 RAML spec —
// GET /accounts and GET /accounts/{accountAddress}. Only the fields Trova
// actually reads are typed out; everything else in the real payload
// (branchBasicInfo, institutionBasicInfo, routings, _links, etc.) is
// ignored rather than modeled, since System.Text.Json just skips unknown
// properties by default.

public class JofsAccountsListResponse
{
    [JsonPropertyName("data")]
    public List<JofsAccount> Data { get; set; } = new();
}

public class JofsAccount
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("accountStatus")]
    public string AccountStatus { get; set; } = string.Empty;

    [JsonPropertyName("accountCurrency")]
    public string AccountCurrency { get; set; } = string.Empty;

    [JsonPropertyName("mainRoute")]
    public JofsRoute? MainRoute { get; set; }

    [JsonPropertyName("availableBalance")]
    public JofsBalance? AvailableBalance { get; set; }
}

public class JofsRoute
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;
}

public class JofsBalance
{
    [JsonPropertyName("balanceAmount")]
    public decimal BalanceAmount { get; set; }

    // "credit" or "debit" — a debit-position balance is money owed, so it
    // gets negated when mapped onto Trova's AvailableBalanceAmount.
    [JsonPropertyName("balancePosition")]
    public string BalancePosition { get; set; } = "credit";
}
