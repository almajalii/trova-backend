namespace TrovaBackend.Services.BankConnection;

// This is the seam: whatever calls IJofsDataProvider doesn't know or care
// whether it's talking to MockJofsDataProvider or a real HTTP+JWS client
// against the sandbox. To go live for real, write RealJofsDataProvider
// implementing this same interface, register it in Program.cs instead of
// the mock, and nothing else in the codebase changes.
//
// Only includes fields confirmed mappable to real JOFS schemas (Accounts,
// Transactions). NumberOfCurrentDebts is deliberately NOT here — the real
// Loans RAML (RFC - Extended Services - Loans v0.4.3) turned out to be a
// loan-origination/application workflow scoped by financial institution,
// not a per-customer "list of existing active loans" service, so it can't
// answer this. Delinquency, debt capacity, and current debts are all
// captured as self-reported input instead (see BankConnection.cs and
// ConnectBankRequest).
public interface IJofsDataProvider
{
    Task<JofsAccountSnapshot> FetchAccountSnapshotAsync(Guid userId, string bankCode);
}

public class JofsAccountSnapshot
{
    public string AccountAddress { get; set; } = string.Empty;
    public string AccountCurrency { get; set; } = "JOD";
    public string AccountStatus { get; set; } = "active";
    public decimal AvailableBalanceAmount { get; set; }

    // Maps to: aggregated JOFS Transactions over a rolling window
    // (GET /accounts/{accountId}/transactions — confirmed real field)
    public decimal AverageMonthlyCashflowChangePercent { get; set; }
}