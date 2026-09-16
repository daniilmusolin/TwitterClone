using System.Text.Json.Serialization;

namespace TweetBackend.Models;

public class Comment {
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = "Anonymous";
    public string AuthorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? ParentCommentId { get; set; }
    public string? ReplyToAuthor { get; set; }
    [JsonPropertyName("replies")]
    public List<Comment> Replies { get; set; } = new();
}