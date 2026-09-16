using System.ComponentModel.DataAnnotations;

namespace TweetBackend.DTOs;

public class CommentUpdateDto {
    [Required]
    [MinLength(1)]
    [MaxLength(1250)]
    public string Content { get; set; } = string.Empty;
}