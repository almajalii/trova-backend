using System.Linq;

namespace TrovaBackend.Services.BankConnection;

// The fixed pool of test customers JoPACC provisioned in the JOFS dev
// sandbox (per their "Values you can use" email). Only Corporate and
// Business customers are listed here — Individual (IND_CUST_*) profiles
// look like personal savings/salary accounts, which doesn't match Trova's
// users (contracting companies), same reasoning MockJofsDataProvider's
// comments already used.
//
// IMPORTANT CAVEAT: every customer in this sandbox belongs to JoPACC's own
// internal test bank ("Bank of JoPACC LTD.", per a confirmed real
// response), not to Arab Bank/Housing Bank/etc. Which Trova bank code the
// user picks has no bearing on which real institution answers in the
// sandbox — that mapping doesn't exist here. This is a sandbox-only
// limitation; real bank-scoped data requires the actual OAuth consent
// flow with each bank, not this static customer list.
public static class JofsSandboxCustomers
{
    // CORP_CUST_001–004: single account (checking/savings/payroll)
    // CORP_CUST_005–007: two accounts (checking + payroll)
    // CORP_CUST_008–009: three accounts (checking + savings + payroll)
    public static readonly string[] Corporate =
    {
        "CORP_CUST_001", "CORP_CUST_002", "CORP_CUST_003", "CORP_CUST_004",
        "CORP_CUST_005", "CORP_CUST_006", "CORP_CUST_007",
        "CORP_CUST_008", "CORP_CUST_009",
    };

    // BUS_CUST_001–004: single account (merchant/operating)
    // BUS_CUST_005–007: two accounts (merchant + operating)
    public static readonly string[] Business =
    {
        "BUS_CUST_001", "BUS_CUST_002", "BUS_CUST_003", "BUS_CUST_004",
        "BUS_CUST_005", "BUS_CUST_006", "BUS_CUST_007",
    };

    // Combined pool used for deterministic per-user assignment. Order
    // matters for reproducibility — do not reorder existing entries once
    // users have been assigned, or reconnecting will hand a returning
    // user a different fake profile than before.
    public static readonly string[] Pool = Corporate.Concat(Business).ToArray();
}
