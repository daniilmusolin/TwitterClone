namespace TweetBackend.DTOs;

public class FollowNotificationDto : NotificationDto {
    public string FromUser { get; set; } = string.Empty;
}