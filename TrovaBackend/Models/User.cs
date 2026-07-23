namespace TrovaBackend.Models;

// New signups can't use the app until an admin reviews the details they
// submitted (CompanyDetails) and approves them. Pending is the default for
// everyone registering from now on; existing accounts were backfilled to
// Approved in the migration that introduced this column so nobody already
// using the app gets locked out.
public static class UserApprovalStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = "user";
    public bool IsBanned { get; set; } = false;

    // ── Admin approval ────────────────────────────────────────────────────
    public string ApprovalStatus { get; set; } = UserApprovalStatus.Pending;
    public string? RejectionReason { get; set; } // set only when ApprovalStatus == Rejected
    public DateTime? ApprovedAt { get; set; } // set when an admin approves or rejects (decision timestamp)

    // ── Email verification ──────────────────────────────────────────────
    public bool IsVerified { get; set; } = false;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiry { get; set; }

    // ── Password reset ──────────────────────────────────────────────────
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // ── Identity verification ───────────────────────────────────────────
    public bool IsIdentityVerified { get; set; } = false;
    public string? NationalId { get; set; }
    public string? IdentityVerificationMethod { get; set; } // "sanad" or "scan"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}