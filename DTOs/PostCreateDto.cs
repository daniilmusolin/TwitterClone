using System.ComponentModel.DataAnnotations;

namespace TweetBackend.DTOs;

public class PostCreateDto {
    [Required]
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;
}