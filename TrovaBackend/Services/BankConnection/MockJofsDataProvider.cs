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
    public Task<JofsAccountSnapshot> FetchAccountSnapshotAsync(Guid userId, string bankCode)
    {
        // Seeded by user + bank so the same person always sees the same
        // demo numbers on repeat calls (consistent for testing/demoing),
        // but two different users connecting to the same bank get
        // different profiles instead of an identical fake balance.
        var seed = HashCode.Combine(userId, bankCode);
        var rng = new Random(seed);

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