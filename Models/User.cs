namespace TweetBackend.Models;

public class User {
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? OAuthId { get; set; }
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> Following { get; set; } = new();
    public List<string> Followers { get; set; } = new();
}