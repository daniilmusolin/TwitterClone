using System.Collections.Concurrent;
using TweetApp.DTOs;
using TweetBackend.DTOs;
using TweetBackend.Models;
using static TweetBackend.DTOs.MessageDto;

namespace TweetBackend.Services;

public class MessageService {
    private readonly ConcurrentDictionary<int, Message> _messages = new();
    private readonly ConcurrentDictionary<string, Chat> _chats = new();
    private readonly UserService _userService;
    private readonly ILogger<MessageService>? _logger;
    private int _nextMessageId = 1;

    public MessageService(UserService userService, ILogger<MessageService>? logger) {
        _userService = userService;
        _logger = logger;
    }

    public async Task<MessageDto> SendMessageAsync(string senderId, string receiverId, string content, int? replyToMessageId = null) {
        if (string.IsNullOrWhiteSpace(content)) return null;
        if (senderId == receiverId) return null;

        var message = new Message {
            Id = Interlocked.Increment(ref _nextMessageId),
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
            ReplyToMessageId = replyToMessageId
        };

        _messages[message.Id] = message;

        var chatId = GetChatId(senderId, receiverId);
        var chat = _chats.GetOrAdd(chatId, new Chat {
            Id = chatId,
            User1Id = senderId,
            User2Id = receiverId,
            LastMessage = content,
            LastMessageAt = DateTime.UtcNow,
            UnreadCount = 0
        });

        chat.LastMessage = content;
        chat.LastMessageAt = DateTime.UtcNow;

        if (chat.User1Id == receiverId) {
            chat.UnreadCount++;
        } else if (chat.User2Id == receiverId) {
            chat.UnreadCount++;
        }

        var sender = await _userService.GetUserByIdAsync(senderId);
        var receiver = await _userService.GetUserByIdAsync(receiverId);

        var result = new MessageDto {
            Id = message.Id,
            SenderId = senderId,
            SenderUsername = sender?.Username ?? "Unknown",
            ReceiverId = receiverId,
            ReceiverUsername = receiver?.Username ?? "Unknown",
            Content = content,
            CreatedAt = message.CreatedAt,
            IsRead = false,
            IsDeleted = false,
            IsDeletedBySender = false,
            IsDeletedByReceiver = false,
            ReplyToMessageId = replyToMessageId
        };

        if (replyToMessageId.HasValue && _messages.TryGetValue(replyToMessageId.Value, out var replyToMsg)) {
            var replyToSender = await _userService.GetUserByIdAsync(replyToMsg.SenderId);
            result.ReplyToMessage = new MessageDto {
                Id = replyToMsg.Id,
                SenderId = replyToMsg.SenderId,
                SenderUsername = replyToSender?.Username ?? "Unknown",
                ReceiverId = replyToMsg.ReceiverId,
                Content = replyToMsg.Content,
                CreatedAt = replyToMsg.CreatedAt,
                IsRead = replyToMsg.IsRead,
                IsDeleted = replyToMsg.IsDeleted,
                IsDeletedBySender = replyToMsg.IsDeletedBySender,
                IsDeletedByReceiver = replyToMsg.IsDeletedByReceiver
            };
        }

        return result;
    }

    public async Task<List<MessageDto>> GetMessagesAsync(string userId, string otherUserId) {
        var messages = _messages.Values
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == userId))
            .Where(m => !m.IsDeleted)
            .Where(m => !(m.SenderId == userId && m.IsDeletedBySender))
            .Where(m => !(m.ReceiverId == userId && m.IsDeletedByReceiver))
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var result = new List<MessageDto>();
        foreach (var m in messages) {
            var sender = await _userService.GetUserByIdAsync(m.SenderId);
            var receiver = await _userService.GetUserByIdAsync(m.ReceiverId);

            var dto = new MessageDto {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderUsername = sender?.Username ?? "Unknown",
                ReceiverId = m.ReceiverId,
                ReceiverUsername = receiver?.Username ?? "Unknown",
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                IsRead = m.IsRead,
                IsDeleted = m.IsDeleted,
                IsDeletedBySender = m.IsDeletedBySender,
                IsDeletedByReceiver = m.IsDeletedByReceiver,
                ReplyToMessageId = m.ReplyToMessageId
            };

            if (m.ReplyToMessageId.HasValue && _messages.TryGetValue(m.ReplyToMessageId.Value, out var replyToMsg)) {
                var replyToSender = await _userService.GetUserByIdAsync(replyToMsg.SenderId);
                dto.ReplyToMessage = new MessageDto {
                    Id = replyToMsg.Id,
                    SenderId = replyToMsg.SenderId,
                    SenderUsername = replyToSender?.Username ?? "Unknown",
                    ReceiverId = replyToMsg.ReceiverId,
                    Content = replyToMsg.Content,
                    CreatedAt = replyToMsg.CreatedAt,
                    IsRead = replyToMsg.IsRead,
                    IsDeleted = replyToMsg.IsDeleted,
                    IsDeletedBySender = replyToMsg.IsDeletedBySender,
                    IsDeletedByReceiver = replyToMsg.IsDeletedByReceiver
                };
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<List<ChatDto>> GetChatsAsync(string userId) {
        var chats = _chats.Values
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        var result = new List<ChatDto>();
        foreach (var chat in chats) {
            var otherId = chat.User1Id == userId ? chat.User2Id : chat.User1Id;
            var otherUser = await _userService.GetUserByIdAsync(otherId);
            if (otherUser == null) continue;

            var unreadCount = _messages.Values
                .Count(m => m.SenderId == otherId && m.ReceiverId == userId && !m.IsRead);

            result.Add(new ChatDto {
                ChatId = chat.Id,
                UserId = otherId,
                Username = otherUser.Username,
                Avatar = otherUser.Avatar,
                LastMessage = chat.LastMessage,
                LastMessageAt = chat.LastMessageAt,
                UnreadCount = unreadCount
            });
        }

        return result;
    }

    public async Task MarkAsReadAsync(string userId, string otherUserId) {
        var messages = _messages.Values
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
            .Where(m => !m.IsDeleted) // удалено у всех
            .Where(m => !(m.SenderId == userId && m.IsDeletedBySender)) 
            .Where(m => !(m.ReceiverId == userId && m.IsDeletedByReceiver)) 
            .ToList();

        foreach (var m in messages) {
            m.IsRead = true;
            m.ReadAt = DateTime.UtcNow;
        }

        var chatId = GetChatId(userId, otherUserId);
        if (_chats.TryGetValue(chatId, out var chat)) {
            var unreadCount = _messages.Values
                .Count(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead
                    && !m.IsDeleted
                    && !(m.SenderId == userId && m.IsDeletedBySender)
                    && !(m.ReceiverId == userId && m.IsDeletedByReceiver));
            chat.UnreadCount = unreadCount;
        }

        await Task.CompletedTask;
    }

    public async Task<int> GetUnreadCountAsync(string userId) {
        return _messages.Values.Count(m => m.ReceiverId == userId && !m.IsRead);
    }

    private string GetChatId(string user1Id, string user2Id) {
        return string.Compare(user1Id, user2Id) < 0
            ? $"{user1Id}_{user2Id}"
            : $"{user2Id}_{user1Id}";
    }

    public async Task<MessageDto> UpdateMessageAsync(int messageId, string userId, string newContent) {
        if (!_messages.TryGetValue(messageId, out var message)) return null;
        if (message.SenderId != userId) return null;
        if (string.IsNullOrWhiteSpace(newContent)) return null;

        message.Content = newContent;
        message.UpdatedAt = DateTime.UtcNow;

        var sender = await _userService.GetUserByIdAsync(message.SenderId);
        var receiver = await _userService.GetUserByIdAsync(message.ReceiverId);

        var result = new MessageDto {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderUsername = sender?.Username ?? "Unknown",
            ReceiverId = message.ReceiverId,
            ReceiverUsername = receiver?.Username ?? "Unknown",
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            IsRead = message.IsRead,
            IsDeleted = message.IsDeleted,
            IsDeletedBySender = message.IsDeletedBySender,
            IsDeletedByReceiver = message.IsDeletedByReceiver,
            ReplyToMessageId = message.ReplyToMessageId
        };

        if (message.ReplyToMessageId.HasValue && _messages.TryGetValue(message.ReplyToMessageId.Value, out var replyToMsg)) {
            var replyToSender = await _userService.GetUserByIdAsync(replyToMsg.SenderId);
            result.ReplyToMessage = new MessageDto {
                Id = replyToMsg.Id,
                SenderId = replyToMsg.SenderId,
                SenderUsername = replyToSender?.Username ?? "Unknown",
                ReceiverId = replyToMsg.ReceiverId,
                Content = replyToMsg.Content,
                CreatedAt = replyToMsg.CreatedAt,
                IsRead = replyToMsg.IsRead,
                IsDeleted = replyToMsg.IsDeleted,
                IsDeletedBySender = replyToMsg.IsDeletedBySender,
                IsDeletedByReceiver = replyToMsg.IsDeletedByReceiver
            };
        }

        return result;
    }

    public async Task<bool> DeleteMessageAsync(int messageId, string userId, bool forEveryone) {
        if (!_messages.TryGetValue(messageId, out var message)) return false;
        if (message.SenderId != userId && message.ReceiverId != userId) return false;
        if (forEveryone && message.SenderId != userId) return false;

        if (forEveryone) {
            message.IsDeleted = true;
            message.Content = "";
        } else {
            if (message.SenderId == userId) {
                message.IsDeletedBySender = true;
            } else if (message.ReceiverId == userId) {
                message.IsDeletedByReceiver = true;
            }
        }

        await Task.CompletedTask;
        return true;
    }

    public Message? GetMessage(int messageId) {
        _messages.TryGetValue(messageId, out var message);
        return message;
    }

    public async Task<bool> DeleteChatAsync(string userId, string otherUserId) {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otherUserId)) return false;
        if (userId == otherUserId) return false;

        var chatId = GetChatId(userId, otherUserId);

        if (_chats.TryGetValue(chatId, out var chat)) {
            _chats.TryRemove(chatId, out _);
        }

        var messagesToDelete = _messages.Values
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == userId))
            .ToList();

        foreach (var msg in messagesToDelete) {
            _messages.TryRemove(msg.Id, out _);
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<List<string>> GetChatUsersAsync(string userId) {
        return _chats.Values
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Select(c => c.User1Id == userId ? c.User2Id : c.User1Id)
            .Distinct()
            .ToList();
    }

    public async Task<SearchResultDto> SearchUsersAsync(string query, string currentUserId, int page = 1, int pageSize = 20) {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) {
            return new SearchResultDto {
                Users = new List<UserDto>(),
                Pagination = new PaginationDto {
                    Total = 0,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 0
                }
            };
        }

        var users = await _userService.SearchUsersAsync(query, page, pageSize);
        var total = await _userService.GetSearchTotalCountAsync(query);
        var totalPages = (int)Math.Ceiling((double)total / pageSize);

        var currentUser = await _userService.GetUserByIdAsync(currentUserId);

        var userDtos = users.Select(user => new UserDto {
            Id = user.Id,
            Username = user.Username,
            Avatar = user.Avatar,
            IsFollowing = currentUser != null && currentUser.Following.Contains(user.Id)
        }).ToList();

        return new SearchResultDto {
            Users = userDtos,
            Pagination = new PaginationDto {
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            }
        };
    }
}