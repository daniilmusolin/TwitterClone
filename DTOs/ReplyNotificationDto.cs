namespace TweetBackend.DTOs;

public class ReplyNotificationDto : NotificationDto {
    public int PostId { get; set; }
    public int ParentCommentId { get; set; }
    public CommentDto Comment { get; set; } = new();
}