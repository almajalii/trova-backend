using System.ComponentModel.DataAnnotations;
using TrovaBackend.Models;

namespace TrovaBackend.DTOs.CompanyDetails;

// ── Request ──────────────────────────────────────────────────────────────
// NOTE — contract change from the original Flutter draft: Sector (single
// string) is now Sectors (list of strings), each constrained to
// TrovaSectors.All. The Flutter form's single "Sector" text field needs to
// become a multi-select against that same fixed list for this to work —
// flag this to whoever owns the frontend.

public class SubmitCompanyDetailsRequest
{
    [Required(ErrorMessage = "Company name is required")]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "At least one sector is required")]
    [MinLength(1, ErrorMessage = "At least one sector is required")]
    public List<string> Sectors { get; set; } = new();

    [Required(ErrorMessage = "Registration number is required")]
    [MaxLength(100)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Years in operation is required")]
    [Range(0, 200, ErrorMessage = "Years in operation must be a realistic value")]
    public int YearsInOperation { get; set; }

    [Required(ErrorMessage = "Team size is required")]
    [Range(1, 1_000_000, ErrorMessage = "Team size must be at least 1")]
    public int TeamSize { get; set; }

    [Required(ErrorMessage = "Annual revenue is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Annual revenue cannot be negative")]
    public decimal AnnualRevenueJod { get; set; }
}

// ── Response ─────────────────────────────────────────────────────────────
// Wrapped as ApiResponse<CompanyDetailsResponse>.Data by the controller, so
// the final JSON shape is exactly what Flutter's CompanyDetailsService
// expects: { "data": { "classification": { "code", "label" } } }

public class CompanyDetailsResponse
{
    public ClassificationDto Classification { get; set; } = new();
}

public class ClassificationDto
{
    public string Code { get; set; } = string.Empty;  // "A" | "B" | "C"
    public string Label { get; set; } = string.Empty; // e.g. "Large Enterprise"
}

// Fuller shape for GET — used by Company Profile / My Score screens later,
// which need to display the raw inputs alongside the classification.
public class CompanyDetailsFullResponse
{
    public string CompanyName { get; set; } = string.Empty;
    public List<string> Sectors { get; set; } = new();
    public string RegistrationNumber { get; set; } = string.Empty;
    public int YearsInOperation { get; set; }
    public int TeamSize { get; set; }
    public decimal AnnualRevenueJod { get; set; }
    public ClassificationDto Classification { get; set; } = new();
}