using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TweetBackend.Services;

namespace TweetBackend.Hubs;

public class NotificationHub : Hub {
    private static readonly ConcurrentDictionary<int, HashSet<string>> _commentViewers = new();
    private static readonly ConcurrentDictionary<string, HashSet<int>> _userCommentSubscriptions = new();
    private readonly ILogger<NotificationHub> _logger;
    private readonly UserService _userService;

    public NotificationHub(ILogger<NotificationHub> logger, UserService userService) {
        _logger = logger;
        _userService = userService;
    }

    public override async Task OnConnectedAsync() {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId)) {
            await _userService.SetUserOnlineAsync(userId);

            await Clients.Group($"user_{userId}").SendAsync("UserStatus", new {
                userId = userId,
                isOnline = true,
                lastSeen = DateTime.UtcNow
            });

            await Clients.Group($"profile_{userId}").SendAsync("UserStatus", new {
                userId = userId,
                isOnline = true,
                lastSeen = DateTime.UtcNow
            });

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId)) {
            await _userService.SetUserOfflineAsync(userId);

            await Clients.Group($"user_{userId}").SendAsync("UserStatus", new {
                userId = userId,
                isOnline = false,
                lastSeen = DateTime.UtcNow
            });

            await Clients.Group($"profile_{userId}").SendAsync("UserStatus", new {
                userId = userId,
                isOnline = false,
                lastSeen = DateTime.UtcNow
            });

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task WatchUser(string userId) {
        var watcherId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(watcherId) || string.IsNullOrEmpty(userId)) return;
        if (watcherId == userId) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"profile_{userId}");

        var user = await _userService.GetUserByIdAsync(userId);
        if (user != null) {
            await Clients.Caller.SendAsync("UserStatus", new {
                userId = userId,
                isOnline = user.IsOnline,
                lastSeen = user.LastSeen
            });
        }

        _logger?.LogInformation($"User {watcherId} is watching profile of {userId}");
    }

    public async Task UnwatchUser(string userId) {
        var watcherId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(watcherId) || string.IsNullOrEmpty(userId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"profile_{userId}");
    }

    public async Task JoinCommentsGroup(int postId) {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"comments_{postId}");

        if (!_commentViewers.ContainsKey(postId)) {
            _commentViewers[postId] = new HashSet<string>();
        }
        _commentViewers[postId].Add(userId);

        if (!_userCommentSubscriptions.ContainsKey(userId)) {
            _userCommentSubscriptions[userId] = new HashSet<int>();
        }
        _userCommentSubscriptions[userId].Add(postId);
        await UpdateCommentsOnlineCount(postId);

        _logger?.LogInformation($"User {userId} joined comments group {postId}");
    }

    public async Task LeaveCommentsGroup(int postId) {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"comments_{postId}");

        if (_commentViewers.TryGetValue(postId, out var viewers)) {
            viewers.Remove(userId);
            if (viewers.Count == 0) {
                _commentViewers.TryRemove(postId, out _);
            }
        }

        if (_userCommentSubscriptions.TryGetValue(userId, out var subscriptions)) {
            subscriptions.Remove(postId);
            if (subscriptions.Count == 0) {
                _userCommentSubscriptions.TryRemove(userId, out _);
            }
        }

        await UpdateCommentsOnlineCount(postId);
        _logger?.LogInformation($"User {userId} left comments group {postId}");
    }

    private async Task UpdateCommentsOnlineCount(int postId) {
        var count = _commentViewers.TryGetValue(postId, out var viewers) ? viewers.Count : 0;
        await Clients.Group($"comments_{postId}").SendAsync("CommentsOnlineCount", new {
            postId = postId,
            count = count
        });
    }

    public async Task GetCommentsOnlineCount(int postId) {
        var count = _commentViewers.TryGetValue(postId, out var viewers) ? viewers.Count : 0;
        await Clients.Caller.SendAsync("CommentsOnlineCount", new {
            postId = postId,
            count = count
        });
    }

    public async Task RestoreSubscriptions() {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        if (_userCommentSubscriptions.TryGetValue(userId, out var subscriptions)) {
            foreach (var postId in subscriptions) {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"comments_{postId}");

                if (!_commentViewers.ContainsKey(postId)) {
                    _commentViewers[postId] = new HashSet<string>();
                }
                _commentViewers[postId].Add(userId);

                await UpdateCommentsOnlineCount(postId);
            }
        }

        _logger?.LogInformation($"Restored subscriptions for user {userId}");
    }

    public async Task SendCommentTypingStatus(int postId, bool isTyping) {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return;

        await Clients.GroupExcept($"comments_{postId}", new[] { Context.ConnectionId })
            .SendAsync("UserTyping", new {
                postId = postId,
                username = user.Username,
                isTyping = isTyping
            });
    }

    public async Task GetConnectionId() {
        var connectionId = Context.ConnectionId;
        Console.WriteLine($"GetConnectionId called, returning: {connectionId}");
        await Clients.Caller.SendAsync("ConnectionId", connectionId);
    }

    public async Task SendChatTypingStatus(string receiverId, bool isTyping) {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        await Clients.Group($"user_{receiverId}").SendAsync("ChatUserTyping", new {
            userId = userId,
            isTyping = isTyping
        });
    }

    public async Task NotifyPostUpdated(int postId, string content, string updatedAt) {
        await Clients.Group($"comments_{postId}").SendAsync("PostUpdated", new {
            postId = postId,
            content = content,
            updatedAt = updatedAt
        });
    }
}