namespace TweetBackend.DTOs;

public class CommentNotificationDto : NotificationDto {
    public int PostId { get; set; }
    public CommentDto Comment { get; set; } = new();
}