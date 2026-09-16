using System.ComponentModel.DataAnnotations;

namespace TweetBackend.DTOs;

public class RegisterDto {
    [Required]
    [MinLength(3)]
    [MaxLength(15)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(4)]
    [MaxLength(30)]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto {
    [Required]
    [MinLength(3)]
    [MaxLength(15)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(4)]
    [MaxLength(30)]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto {
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}