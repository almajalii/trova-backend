namespace TrovaBackend.Services.BankConnection;

// Stand-in for a real JOFS sandbox client. Generates plausible, deterministic
// numbers per bank (same bank always produces the same demo snapshot, so
// testing/demoing is consistent) rather than pure random noise on every call.
//
// TODO when/if real sandbox access exists: implement RealJofsDataProvider
// against IJofsDataProvider —
//   AvailableBalanceAmount   <- GET /accounts/{accountAddress} -> availableBalance.amount
//   NumberOfCurrentDebts     <- GET /institution/loans?loanStatus=active -> count(data)
//   AverageMonthlyCashflowChangePercent <- GET /accounts/{accountId}/transactions,
//     aggregate transactionAmount by month, compute % change
// All three need Authorization + x-interactions-id + x-idempotency-key +
// x-jws-signature headers. Swap the DI registration in Program.cs from this
// class to the real one — nothing else changes.
public class MockJofsDataProvider : IJofsDataProvider
{
    public Task<JofsAccountSnapshot> FetchAccountSnapshotAsync(string bankCode)
    {
        // Seeded by bank code so the same bank always yields the same demo
        // numbers, instead of a different score every time someone re-tests.
        var rng = new Random(bankCode.GetHashCode());

        var snapshot = new JofsAccountSnapshot
        {
            AccountAddress = $"JO{rng.Next(10, 99)}{bankCode.ToUpperInvariant()}{rng.Next(1000000, 9999999)}",
            AccountCurrency = "JOD",
            AccountStatus = "active",
            AvailableBalanceAmount = rng.Next(80_000, 350_000),
            NumberOfCurrentDebts = rng.Next(0, 4),
            AverageMonthlyCashflowChangePercent = rng.Next(-5, 10)
        };

        return Task.FromResult(snapshot);
    }
}
