namespace TrovaBackend.Services.Guarantees;

// Bound from the "Storage" section of appsettings.json. Local disk is a
// placeholder — the app's PublishProfiles show this deploys to Azure App
// Service via Zip Deploy, and App Service's local filesystem is NOT
// durable storage (content can be wiped on redeploy/scale-out). Swap
// SaveFileAsync in GuaranteeService for Azure Blob Storage before this
// goes anywhere near production; flagged rather than silently shipped as
// if it were durable.
public class GuaranteeStorageOptions
{
    // Relative to the app's content root unless an absolute path is given.
    public string GuaranteeDocumentsPath { get; set; } = "App_Data/guarantee-documents";
}
