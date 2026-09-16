using TweetBackend.Models;

public class Post {
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = "Anonymous";
    public string AuthorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<Comment> Comments { get; set; } = new();
    public HashSet<string> Likes { get; set; } = new();
    public int Views { get; set; }
    public List<Repost> Reposts { get; set; } = new();
}