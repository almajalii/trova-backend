namespace TrovaBackend.Models;

// ── Field provenance ────────────────────────────────────────────────────
// Split into two groups, matching the "From Bank (via Open Finance)"
// divider already in the Figma My Score screen:
//
// BANK-VERIFIED — pulled from the JOFS provider (mock today, real sandbox
// client later, same IJofsDataProvider interface either way):
//   - AccountAddress/Currency/Status/AvailableBalanceAmount → JOFS Accounts
//     schema (accountId, accountCurrency, accountStatus, availableBalance.amount)
//   - AverageMonthlyCashflowChangePercent → JOFS Transactions schema,
//     aggregated transactionAmount over time (confirmed field-level)
//
// SELF-REPORTED — captured directly from the user on the Connect Bank
// screen, NOT from JOFS:
//   - RemainingDebtCapacityJod, NumberOfDelinquentDebts — confirmed by
//     reading the real Loans schema: no delinquency status and no
//     credit-limit/capacity field anywhere in JOFS Accounts, Balances, or
//     Loans. RecommendedMaxDebtCapacityJod isn't stored per-user at all —
//     it's a Trova policy constant, see ScoringOptions.
//   - NumberOfCurrentDebts — moved here after actually reading the real
//     Loans RAML (RFC - Extended Services - Loans v0.4.3): it's a
//     loan-origination/application workflow (submit application, accept/
//     reject offers, upload documents), scoped by financial institution,
//     not a per-customer "list of existing active loans" service. No JOFS
//     endpoint answers "how many active debts does this customer have"
//     for a consented customer, so this can't be bank-verified today.
public class BankConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // One connected bank account per user for MVP scope.
    public Guid UserId { get; set; }

    public string BankCode { get; set; } = string.Empty;  // e.g. "arab_bank"
    public string BankName { get; set; } = string.Empty;  // e.g. "Arab Bank"

    // ── Bank-verified ─────────────────────────────────────────────────
    public string AccountAddress { get; set; } = string.Empty; // accountId / IBAN
    public string AccountCurrency { get; set; } = "JOD";
    public string AccountStatus { get; set; } = "active";
    public decimal AvailableBalanceAmount { get; set; }
    public decimal AverageMonthlyCashflowChangePercent { get; set; }

    // ── Self-reported (declared by the user at Connect Bank time) ──────
    public decimal RemainingDebtCapacityJod { get; set; }
    public int NumberOfDelinquentDebts { get; set; }
    public int NumberOfCurrentDebts { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
