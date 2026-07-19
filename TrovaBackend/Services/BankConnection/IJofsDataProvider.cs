namespace TrovaBackend.Services.BankConnection;

// This is the seam: whatever calls IJofsDataProvider doesn't know or care
// whether it's talking to MockJofsDataProvider or a real HTTP+JWS client
// against the sandbox. To go live for real, write RealJofsDataProvider
// implementing this same interface, register it in Program.cs instead of
// the mock, and nothing else in the codebase changes.
//
// Only includes fields confirmed mappable to real JOFS schemas (Accounts,
// Loans, Transactions) — delinquency and debt capacity are NOT here since
// no JOFS service exposes either concept; those are captured as
// self-reported input instead (see BankConnection.cs and ConnectBankRequest).
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

    // Maps to: count of JOFS Loans where loanStatus == "active"
    // (GET /institution/loans?loanStatus=active — confirmed real field)
    public int NumberOfCurrentDebts { get; set; }

    // Maps to: aggregated JOFS Transactions over a rolling window
    // (GET /accounts/{accountId}/transactions — confirmed real field)
    public decimal AverageMonthlyCashflowChangePercent { get; set; }
}