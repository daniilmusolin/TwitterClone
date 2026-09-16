namespace TweetApp.DTOs {
    public class UserDto {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public bool IsFollowing { get; set; }
    }
}
