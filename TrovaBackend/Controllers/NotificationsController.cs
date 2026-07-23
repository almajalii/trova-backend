using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Common;
using TrovaBackend.DTOs.Notifications;
using TrovaBackend.Services.Notifications;

namespace TrovaBackend.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _notificationService.GetForUserAsync(userId);
        return Ok(new ApiResponse<List<NotificationDto>>
        {
            Success = true,
            Message = "Notifications retrieved successfully.",
            Data = result
        });
    }
}
