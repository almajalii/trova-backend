using TrovaBackend.DTOs.Notifications;

namespace TrovaBackend.Services.Notifications;

public interface INotificationService
{
    // Fire-and-forget from the event that triggered it (bid submitted,
    // guarantee issued, review received, score increased). Callers should
    // not let a notification failure block the underlying action —
    // wrap in try/catch same as the email-sending convention elsewhere.
    Task CreateAsync(Guid userId, string type, string message);

    // Persisted notifications for this user, newest first, plus
    // GUARANTEE_EXPIRING entries computed live (not persisted — see
    // NotificationService for why) for guarantees nearing expiry.
    Task<List<NotificationDto>> GetForUserAsync(Guid userId);
}
