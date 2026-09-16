using System.ComponentModel.DataAnnotations;

namespace TweetBackend.DTOs;

public class PostUpdateDto {
    [Required]
    [MinLength(1)]
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;
}