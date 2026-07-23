namespace TrovaBackend.Services.BankConnection;

// Stand-in for a real JOFS sandbox client. Generates plausible, deterministic
// numbers per bank (same bank always produces the same demo snapshot, so
// testing/demoing is consistent) rather than pure random noise on every call.
//
// See RealJofsDataProvider for the actual live implementation —
//   AvailableBalanceAmount   <- GET /accounts/{accountAddress} -> availableBalance.amount
//   AverageMonthlyCashflowChangePercent <- GET /accounts/{accountId}/transactions,
//     aggregate transactionAmount by month, compute % change
// NumberOfCurrentDebts is NOT generated here — it's self-reported on
// ConnectBankRequest now, not part of the bank-verified snapshot. (Earlier
// version of this comment assumed a JOFS Loans lookup for it; reading the
// real Loans RAML disproved that — see IJofsDataProvider.cs.)
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

        // Balance + status distribution below matches the real seed data
        // stats JoPACC shared for the JOFS sandbox's Business/Corporate
        // test accounts (BUS_CUST_*/CORP_CUST_* — the categories that
        // match a contracting company, not IND_CUST_* individuals):
        //   Zero balance: 23% (mostly closed accounts)
        //   Negative/overdraft: 24% (mostly suspended)
        //   Low (1,500-25,000 JOD): ~18%
        //   Normal (25,000-150,000 JOD): ~18%
        //   High (150,000+ JOD, typically corporate): ~17%
        // Overall status split across all sandbox accounts: Active 53%,
        // Suspended 24%, Closed 23% — roughly tracked here by tying status
        // to the balance branch, same pattern the real data shows.
        var roll = rng.NextDouble();
        decimal balance;
        string status;

        if (roll < 0.23)
        {
            balance = 0m;
            status = "closed";
        }
        else if (roll < 0.47)
        {
            balance = -rng.Next(200, 8_000);
            status = "suspended";
        }
        else if (roll < 0.65)
        {
            balance = rng.Next(1_500, 25_000);
            status = "active";
        }
        else if (roll < 0.83)
        {
            balance = rng.Next(25_000, 150_000);
            status = "active";
        }
        else
        {
            balance = rng.Next(150_000, 500_000);
            status = "active";
        }

        var snapshot = new JofsAccountSnapshot
        {
            AccountAddress = $"JO{rng.Next(10, 99)}{bankCode.ToUpperInvariant()}{rng.Next(1000000, 9999999)}",
            AccountCurrency = "JOD",
            AccountStatus = status,
            AvailableBalanceAmount = balance,
            AverageMonthlyCashflowChangePercent = rng.Next(-5, 10)
        };

        return Task.FromResult(snapshot);
    }
}