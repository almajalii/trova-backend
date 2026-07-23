using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Notifications;
using TrovaBackend.Models;

namespace TrovaBackend.Services.Notifications;

public class NotificationService : INotificationService
{
    // Guarantees inside this window of their expiry show up as a
    // GUARANTEE_EXPIRING notification. Not configurable via appsettings
    // for now — hardcode-and-flag rather than add a config surface for a
    // single number with no other consumer.
    private const int ExpiringWithinDays = 30;

    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(Guid userId, string type, string message)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> GetForUserAsync(Guid userId)
    {
        var persisted = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Type = n.Type,
                Message = n.Message,
                Timestamp = ToWireTimestamp(n.CreatedAt),
            })
            .ToListAsync();

        var expiring = await BuildExpiringGuaranteeNotificationsAsync(userId);

        return persisted
            .Concat(expiring)
            .OrderByDescending(n => n.Timestamp, StringComparer.Ordinal) // ISO-8601 sorts lexicographically
            .ToList();
    }

    // GUARANTEE_EXPIRING isn't persisted — there's no background job/
    // scheduler in this codebase to periodically insert-then-expire these
    // rows, and persisting them at read time would create duplicates on
    // every call. Computed fresh each request instead: still correct,
    // just costs a query instead of being free-reads-from-a-table.
    // Timestamp is anchored to (ValidityExpiry - ExpiringWithinDays) so it
    // has a stable sort position across repeated calls, rather than
    // jumping around based on when the request happened to land.
    private async Task<List<NotificationDto>> BuildExpiringGuaranteeNotificationsAsync(Guid contractorId)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(ExpiringWithinDays);

        var expiringApplications = await _db.GuaranteeApplications
            .Where(g => g.ContractorId == contractorId
                     && g.Status == GuaranteeStatus.Approved
                     && g.ValidityExpiry > now
                     && g.ValidityExpiry <= horizon)
            .ToListAsync();

        if (expiringApplications.Count == 0)
            return new List<NotificationDto>();

        var projectIds = expiringApplications.Select(g => g.ProjectId).Distinct().ToList();
        var projectTitles = await _db.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title);

        return expiringApplications.Select(g =>
        {
            var daysLeft = (int)Math.Ceiling((g.ValidityExpiry - now).TotalDays);
            var title = projectTitles.GetValueOrDefault(g.ProjectId, "your project");

            return new NotificationDto
            {
                Type = NotificationType.GuaranteeExpiring,
                Message = $"Your guarantee for {title} expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")}.",
                Timestamp = ToWireTimestamp(g.ValidityExpiry.AddDays(-ExpiringWithinDays)),
            };
        }).ToList();
    }

    private static string ToWireTimestamp(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
