using System.ComponentModel.DataAnnotations;

namespace TrovaBackend.DTOs.Projects;

// ── Requests ──────────────────────────────────────────────────────────────

// Matches the Post Project screen's draft object — POST /api/projects.
// No `status` field: every new project starts as OPEN_FOR_BIDS server-side.
public class PostProjectRequest
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sector is required")]
    public string Sector { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contract value is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Contract value cannot be negative")]
    public decimal ContractValue { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    [MaxLength(10)]
    public string Currency { get; set; } = "JOD";

    [Required(ErrorMessage = "Duration is required")]
    public string Duration { get; set; } = string.Empty;

    [Required(ErrorMessage = "Milestones are required")]
    public string Milestones { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bid submission deadline is required")]
    public DateTime BidSubmissionDeadline { get; set; }

    [Required(ErrorMessage = "Minimum required score is required")]
    [Range(0, 100, ErrorMessage = "Minimum required score must be between 0 and 100")]
    public int MinimumRequiredScore { get; set; }

    [Required(ErrorMessage = "Minimum classification is required")]
    [RegularExpression("^(A|B|C)$", ErrorMessage = "Minimum classification must be 'A', 'B', or 'C'")]
    public string MinimumClassification { get; set; } = string.Empty;

    [Required(ErrorMessage = "Guarantee type is required")]
    public string GuaranteeType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Payment terms are required")]
    public string PaymentTerms { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = string.Empty;
}

// ── Responses ─────────────────────────────────────────────────────────────

public class PostProjectResponse
{
    public string ProjectId { get; set; } = string.Empty; // TRV-PRJ-XXXXX
}