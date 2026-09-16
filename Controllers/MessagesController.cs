using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TweetApp.DTOs;
using TweetBackend.DTOs;
using TweetBackend.Hubs;
using TweetBackend.Services;
using static TweetBackend.DTOs.MessageDto;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : BaseController {
    private readonly MessageService _messageService;
    private readonly UserService _userService;
    private readonly IHubContext<NotificationHub> _hubContext;

    public MessagesController(
        MessageService messageService,
        UserService userService,
        IHubContext<NotificationHub> hubContext) {
        _messageService = messageService;
        _userService = userService;
        _hubContext = hubContext;
    }

    [HttpGet("chats")]
    [Authorize]
    public async Task<IActionResult> GetChats() {
        if (CurrentUser == null) return Unauthorized();
        var chats = await _messageService.GetChatsAsync(CurrentUserId!);
        return Ok(chats);
    }

    [HttpGet("{userId}")]
    [Authorize]
    public async Task<IActionResult> GetMessages(string userId) {
        if (CurrentUser == null) return Unauthorized();
        var messages = await _messageService.GetMessagesAsync(CurrentUserId!, userId);
        return Ok(messages);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("SendMessage")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto) {
        if (CurrentUser == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Content)) {
            return BadRequest(new { error = "Content is required" });
        }

        if (dto.Content.Length > 3000) {
            return BadRequest(new { error = "Message cannot exceed 3000 characters" });
        }

        var message = await _messageService.SendMessageAsync(CurrentUserId!, dto.ReceiverId, dto.Content, dto.ReplyToMessageId);
        if (message == null) {
            return BadRequest(new { error = "Cannot send message" });
        }

        await _hubContext.Clients.Group($"user_{dto.ReceiverId}")
            .SendAsync("NewMessage", message);

        return Ok(message);
    }

    [HttpPost("read/{userId}")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(string userId) {
        if (CurrentUser == null) return Unauthorized();
        await _messageService.MarkAsReadAsync(CurrentUserId!, userId);
        return Ok(new { success = true });
    }

    [HttpGet("unread-count")]
    [Authorize]
    public async Task<IActionResult> GetUnreadCount() {
        if (CurrentUser == null) return Unauthorized();
        var count = await _messageService.GetUnreadCountAsync(CurrentUserId!);
        return Ok(new { count });
    }

    [HttpPut("{messageId}")]
    [Authorize]
    public async Task<IActionResult> UpdateMessage(int messageId, [FromBody] UpdateMessageDto dto) {
        if (CurrentUser == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Content)) {
            return BadRequest(new { error = "Content is required" });
        }

        if (dto.Content.Length > 3000) {
            return BadRequest(new { error = "Message cannot exceed 3000 characters" });
        }

        var updated = await _messageService.UpdateMessageAsync(messageId, CurrentUserId!, dto.Content);
        if (updated == null) {
            return BadRequest(new { error = "Cannot update message" });
        }

        var receiverId = updated.ReceiverId;

        await _hubContext.Clients.Group($"user_{receiverId}")
            .SendAsync("MessageUpdated", new {
                messageId = updated.Id,
                content = updated.Content,
                updatedAt = updated.UpdatedAt,
                replyToMessage = updated.ReplyToMessage
            });

        await _hubContext.Clients.Group($"user_{CurrentUserId}")
            .SendAsync("MessageUpdated", new {
                messageId = updated.Id,
                content = updated.Content,
                updatedAt = updated.UpdatedAt,
                replyToMessage = updated.ReplyToMessage
            });

        return Ok(updated);
    }

    [HttpDelete("{messageId}")]
    [Authorize]
    public async Task<IActionResult> DeleteMessage(int messageId, [FromQuery] bool forEveryone = false) {
        if (CurrentUser == null) return Unauthorized();

        var result = await _messageService.DeleteMessageAsync(messageId, CurrentUserId!, forEveryone);
        if (!result) {
            return BadRequest(new { error = "Cannot delete message" });
        }

        var message = _messageService.GetMessage(messageId);
        if (message == null) return Ok(new { success = true });

        var receiverId = message.ReceiverId;

        if (forEveryone) {
            await _hubContext.Clients.Group($"user_{receiverId}")
                .SendAsync("MessageDeleted", new { messageId, forEveryone = true });
            await _hubContext.Clients.Group($"user_{CurrentUserId}")
                .SendAsync("MessageDeleted", new { messageId, forEveryone = true });
        } else {
            await _hubContext.Clients.Group($"user_{receiverId}")
                .SendAsync("MessageDeleted", new { messageId, forEveryone = false });
        }

        return Ok(new { success = true, messageId, forEveryone });
    }

    [HttpDelete("chat/{userId}")]
    [Authorize]
    public async Task<IActionResult> DeleteChat(string userId) {
        if (CurrentUser == null) return Unauthorized();
        var result = await _messageService.DeleteChatAsync(CurrentUserId!, userId);
        if (!result) return BadRequest();

        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ChatDeleted", new { userId = CurrentUserId, otherUserId = userId });

        return Ok(new { success = true });
    }

    [HttpGet("search")]
    [Authorize]
    [EnableRateLimiting("Search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
        if (CurrentUser == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) {
            return Ok(new SearchResultDto {
                Users = new List<UserDto>(),
                Pagination = new PaginationDto {
                    Total = 0,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 0
                }
            });
        }

        var result = await _messageService.SearchUsersAsync(query, CurrentUserId!, page, pageSize);
        return Ok(result);
    }

    [HttpGet("status/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetUserStatus(string userId) {
        if (CurrentUser == null) return Unauthorized();

        var isOnline = await _userService.IsUserOnlineAsync(userId);
        var lastSeen = await _userService.GetUserLastSeenAsync(userId);

        return Ok(new {
            status = isOnline ? "online" : "offline",
            lastSeen = lastSeen
        });
    }
}