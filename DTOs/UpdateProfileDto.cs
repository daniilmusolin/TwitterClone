using System.ComponentModel.DataAnnotations;

namespace TweetApp.DTOs {
    public class UpdateProfileDto {
        [MinLength(0)]
        [MaxLength(180)]
        public string? Bio { get; set; }
        public string? Avatar { get; set; }
    }
}
