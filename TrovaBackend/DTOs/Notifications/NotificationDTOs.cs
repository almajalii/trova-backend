namespace TrovaBackend.DTOs.Notifications;

// GET /api/notifications response shape — matches notification_model.dart's
// expected fields exactly: type (uppercase snake case), message
// (already-formatted display string), timestamp (ISO-8601 UTC).
public class NotificationDto
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty; // "2026-07-21T14:00:00Z"
}
