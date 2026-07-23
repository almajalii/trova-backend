namespace TrovaBackend.Services.BankConnection;

// Bound from appsettings.json "Jofs" section. Drives RealJofsDataProvider.
//
// UseMock stays true until this is filled in for real — flips the DI
// registration in Program.cs between MockJofsDataProvider and
// RealJofsDataProvider without touching any other code.
public class JofsApiOptions
{
    public bool UseMock { get; set; } = true;

    // Each JOFS service (Accounts, Transactions, Loans, ...) lives at its
    // own base path on the gateway — there's no single shared root, so
    // these are kept separate rather than one BaseUrl + relative paths.
    // e.g. "http://jpcjofsdev.apigw-az-eu.webmethods.io/gateway/Accounts/v0.4.3"
    public string AccountsBaseUrl { get; set; } = string.Empty;

    // e.g. "http://jpcjofsdev.apigw-az-eu.webmethods.io/gateway/Transactions/v0.4.3"
    public string TransactionsBaseUrl { get; set; } = string.Empty;

    // Full header value, e.g. "Basic amFuYW1hamFsaWs2..." — copy straight
    // from the working Postman/sandbox call.
    public string AuthorizationHeader { get; set; } = string.Empty;

    // Sandbox dev values — real x-jws-signature needs an actual JWS
    // detached signature over the request per the JOF security spec, but
    // the dev gateway accepts a literal "X" for now (confirmed via manual
    // sandbox call). Swap this for real signing before anything but dev.
    public string JwsSignaturePlaceholder { get; set; } = "X";

    // Optional explicit override: pin a specific Trova bank code to a
    // specific sandbox test customer ID (e.g. for a scripted demo).
    // Usually left empty — when a bank code has no entry here,
    // RealJofsDataProvider deterministically picks a customer from
    // JofsSandboxCustomers.Pool based on (userId, bankCode), so different
    // real users spread across the sandbox's Corporate+Business test
    // customers instead of all landing on the same one.
    public Dictionary<string, string> BankCustomerIds { get; set; } = new();
}
