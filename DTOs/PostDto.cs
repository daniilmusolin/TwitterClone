public class PostDto {
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int LikesCount { get; set; }
    public int RepostsCount { get; set; }
    public int CommentsCount { get; set; }
    public int Views { get; set; }
    public bool IsLiked { get; set; }      
    public bool IsReposted { get; set; } 
}