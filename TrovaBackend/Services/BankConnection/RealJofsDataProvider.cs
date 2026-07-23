using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TrovaBackend.Services.BankConnection;

// Real implementation of IJofsDataProvider against JoPACC's JOFS sandbox
// (confirmed working end-to-end: manual GET /accounts and GET
// /accounts/{id}/transactions calls, and a real POST /connect through this
// exact provider, all returned real sandbox data — see the account address
// format JO27CBJO... as the tell that distinguishes real from mock).
//
// STATUS:
//   - Accounts (balance, status, currency, address) — wired up.
//   - Transactions (cashflow trend) — wired up.
//   - NumberOfCurrentDebts is NOT part of this provider's contract at all
//     (see IJofsDataProvider.cs) — it's self-reported on ConnectBankRequest
//     instead, since the real Loans RAML turned out to be a loan-origination
//     workflow, not a per-customer existing-debts lookup.
//
// Registered in Program.cs only when Jofs:UseMock is false.
public class RealJofsDataProvider : IJofsDataProvider
{
    private readonly HttpClient _http;
    private readonly JofsApiOptions _options;
    private readonly ILogger<RealJofsDataProvider> _logger;

    public RealJofsDataProvider(HttpClient http, IOptions<JofsApiOptions> options, ILogger<RealJofsDataProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JofsAccountSnapshot> FetchAccountSnapshotAsync(Guid userId, string bankCode)
    {
        var customerId = ResolveCustomerId(userId, bankCode);

        var account = await FetchPrimaryAccountAsync(customerId, bankCode);

        var balance = account.AvailableBalance?.BalanceAmount ?? 0m;
        if (string.Equals(account.AvailableBalance?.BalancePosition, "debit", StringComparison.OrdinalIgnoreCase))
            balance = -balance;

        var accountId = account.AccountId;
        var cashflowChangePercent = await ComputeAverageMonthlyCashflowChangePercentAsync(customerId, accountId);

        return new JofsAccountSnapshot
        {
            AccountAddress = account.MainRoute?.Address ?? account.AccountId,
            AccountCurrency = string.IsNullOrEmpty(account.AccountCurrency) ? "JOD" : account.AccountCurrency,
            AccountStatus = string.IsNullOrEmpty(account.AccountStatus) ? "active" : account.AccountStatus,
            AvailableBalanceAmount = balance,
            AverageMonthlyCashflowChangePercent = cashflowChangePercent,
        };
    }

    // Picks which sandbox test customer this user+bank maps to. Explicit
    // overrides in appsettings (Jofs:BankCustomerIds) win if present —
    // useful for pinning a specific bank code to a specific test customer
    // for a demo. Otherwise, seeds off (userId, bankCode) the same way
    // MockJofsDataProvider does, so the same user always lands on the same
    // sandbox customer across reconnects, but different users spread
    // across the whole Corporate+Business pool instead of all landing on
    // one hardcoded customer.
    private string ResolveCustomerId(Guid userId, string bankCode)
    {
        if (_options.BankCustomerIds.TryGetValue(bankCode, out var mapped) && !string.IsNullOrEmpty(mapped))
            return mapped;

        var pool = JofsSandboxCustomers.Pool;
        var seed = HashCode.Combine(userId, bankCode);
        var index = Math.Abs(seed) % pool.Length;
        return pool[index];
    }

    private async Task<JofsAccount> FetchPrimaryAccountAsync(string customerId, string bankCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.AccountsBaseUrl}/accounts");
        ApplyStandardHeaders(request, customerId);

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JofsAccountsListResponse>()
            ?? throw new InvalidOperationException("JOFS /accounts returned an empty or unparseable body.");

        // A test customer can have multiple accounts (savings, checking,
        // etc.) — take the first active one if there is one, otherwise
        // just the first account returned, so a suspended/closed test
        // customer still produces a snapshot instead of throwing.
        var account = body.Data.FirstOrDefault(a => a.AccountStatus == "active") ?? body.Data.FirstOrDefault();

        if (account == null)
        {
            _logger.LogWarning("JOFS /accounts returned no accounts for customer {CustomerId} (bank {BankCode})", customerId, bankCode);
            throw new InvalidOperationException($"No accounts found for bank '{bankCode}'.");
        }

        return account;
    }

    // Buckets net cashflow (credits minus debits) by calendar month across
    // the most recent transactions, then averages the month-over-month %
    // change across whatever consecutive month pairs are available. Needs
    // at least 2 months of data to mean anything — returns 0 otherwise (a
    // brand-new/sparse test account shouldn't silently show as a wild
    // swing either direction).
    //
    // Deliberately does NOT filter by settlementDateFrom/To relative to
    // "now" — confirmed via manual sandbox testing that JOFS dev seed data
    // is dated in the past (2023), not near real time, so a "last 90 days
    // from today" filter would silently return zero rows against real
    // sandbox data. Instead this just asks for the most recent N
    // transactions (sort=desc, limit) and lets whatever dates come back
    // define the months compared — works the same whether the account's
    // most recent activity was last week or three years ago.
    private async Task<decimal> ComputeAverageMonthlyCashflowChangePercentAsync(string customerId, string accountId)
    {
        var url = $"{_options.TransactionsBaseUrl}/accounts/{Uri.EscapeDataString(accountId)}/transactions" +
                  $"?limit=100&sort=desc";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyStandardHeaders(request, customerId);

        using var response = await _http.SendAsync(request);

        // A missing/empty transaction history shouldn't fail the whole
        // bank connection — just contributes 0 to that one factor.
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("JOFS /accounts/{AccountId}/transactions returned {Status}", accountId, response.StatusCode);
            return 0m;
        }

        var body = await response.Content.ReadFromJsonAsync<JofsTransactionsListResponse>();
        if (body == null || body.Data.Count == 0)
            return 0m;

        var monthlyNet = body.Data
            .Where(t => t.SettlementDateTime.HasValue && t.TransactionAmount != null)
            .GroupBy(t => new DateTime(t.SettlementDateTime!.Value.Year, t.SettlementDateTime.Value.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => g.Sum(t => string.Equals(t.TransactionType, "debit", StringComparison.OrdinalIgnoreCase)
                ? -t.TransactionAmount!.Amount
                : t.TransactionAmount!.Amount))
            .ToList();

        if (monthlyNet.Count < 2)
            return 0m;

        var changes = new List<decimal>();
        for (var i = 1; i < monthlyNet.Count; i++)
        {
            var previous = monthlyNet[i - 1];
            if (previous == 0m)
                continue; // avoid divide-by-zero; skip a month that started at exactly 0 net

            changes.Add((monthlyNet[i] - previous) / Math.Abs(previous) * 100m);
        }

        return changes.Count > 0 ? Math.Round(changes.Average(), 1) : 0m;
    }

    private void ApplyStandardHeaders(HttpRequestMessage request, string customerId)
    {
        if (!string.IsNullOrEmpty(_options.AuthorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", _options.AuthorizationHeader);

        request.Headers.TryAddWithoutValidation("x-customer-id", customerId);
        request.Headers.TryAddWithoutValidation("x-jws-signature", _options.JwsSignaturePlaceholder);

        // Required by the spec on every call, and meant to correlate a
        // request with its response / detect duplicate retries — a fresh
        // GUID per call is fine for the dev sandbox where nothing checks
        // for actual idempotent replay.
        request.Headers.TryAddWithoutValidation("x-interactions-id", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("x-idempotency-key", Guid.NewGuid().ToString());

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
