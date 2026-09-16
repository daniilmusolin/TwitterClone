using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : BaseController {
    private readonly TweetStore _store;

    public NotificationsController(TweetStore store) {
        _store = store;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetNotifications() {
        if (CurrentUser == null) return Unauthorized();
        var notifications = await _store.GetNotificationsAsync(CurrentUserId!);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    [Authorize]
    public async Task<IActionResult> GetUnreadCount() {
        if (CurrentUser == null) return Unauthorized();
        var count = await _store.GetUnreadCountAsync(CurrentUserId!);
        return Ok(new { count });
    }

    [HttpPost("{id}/read")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(int id) {
        if (CurrentUser == null) return Unauthorized();
        await _store.MarkNotificationAsReadAsync(id);
        return Ok();
    }

    [HttpPost("read-all")]
    [Authorize]
    public async Task<IActionResult> MarkAllAsRead() {
        if (CurrentUser == null) return Unauthorized();
        await _store.MarkAllAsReadAsync(CurrentUserId!);
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteNotification(int id) {
        if (CurrentUser == null) return Unauthorized();
        await _store.DeleteNotificationAsync(CurrentUserId!, id);
        return Ok();
    }

    [HttpDelete("all")]
    [Authorize]
    public async Task<IActionResult> DeleteAll() {
        if (CurrentUser == null) return Unauthorized();
        await _store.DeleteAllNotificationsAsync(CurrentUserId!);
        return Ok();
    }
}