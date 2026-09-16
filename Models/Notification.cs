namespace TweetBackend.Models;

public class Notification {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FromUser { get; set; } = string.Empty;
    public string? PostId { get; set; }
    public string? CommentId { get; set; }
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}