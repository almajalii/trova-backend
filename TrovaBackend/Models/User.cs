namespace TrovaAPI.Models;

/// <summary>
/// Base user entity. Extend with Trova-specific fields (role, profile info, etc.)
/// once the domain is defined.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }

    // Placeholder for future role-based access (e.g. "user", "admin").
    // Adjust once Trova's roles are known.
    public string Role { get; set; } = "user";

    public bool IsBanned { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
