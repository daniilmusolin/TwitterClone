using System.ComponentModel.DataAnnotations;

namespace TweetBackend.DTOs;

public class CommentCreateDto {
    [Required]
    [MinLength(1)]
    [MaxLength(1250)]
    public string Content { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    public string? ReplyToAuthor { get; set; } 
}