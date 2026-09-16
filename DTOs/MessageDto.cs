using System.ComponentModel.DataAnnotations;
using TweetApp.DTOs;

namespace TweetBackend.DTOs;

public class MessageDto {
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string ReceiverUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDeletedBySender { get; set; }
    public bool IsDeletedByReceiver { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ReplyToMessageId { get; set; }
    public MessageDto ReplyToMessage { get; set; }
}

public class SendMessageDto {
    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [Required]
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;

    public int? ReplyToMessageId { get; set; }
}

public class ChatDto {
    public string ChatId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class UpdateMessageDto {
    [Required]
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;
}

public class SearchResultDto {
    public List<UserDto> Users { get; set; } = new();
    public PaginationDto Pagination { get; set; } = new();
}

public class PaginationDto {
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}