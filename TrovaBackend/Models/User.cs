namespace TrovaBackend.Models;

/// <summary>
/// Base user entity. Extend with Trova-specific fields (contractor profile,
/// company info, etc.) once those screens are defined.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Not collected at signup per current frontend — keep optional for later.
    public string? Phone { get; set; }

    // Placeholder for future role-based access (e.g. "contractor", "admin").
    public string Role { get; set; } = "user";

    public bool IsBanned { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}