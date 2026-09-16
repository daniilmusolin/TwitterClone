namespace TweetBackend.DTOs;

public abstract class NotificationDto {
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string FromUser { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}