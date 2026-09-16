using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TweetBackend.DTOs;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController {
    private readonly UserService _userService;

    public AuthController(UserService userService) {
        _userService = userService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("Register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto) {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { error = "Username and password required" });

        if (dto.Username.Length < 3)
            return BadRequest(new { error = "Username must be at least 3 characters" });

        if (dto.Password.Length < 4)
            return BadRequest(new { error = "Password must be at least 4 characters" });

        var user = await _userService.RegisterAsync(dto.Username, dto.Password);
        if (user == null) {
            return Conflict(new { error = "Username already exists" });
        }

        var token = await _userService.CreateSessionAsync(user.Id);
        var refreshToken = await _userService.CreateRefreshTokenAsync(user.Id);

        SetRefreshTokenCookie(refreshToken);

        return Ok(new AuthResponseDto {
            Id = user.Id,
            Username = user.Username,
            Token = token
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto) {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { error = "Username and password required" });

        var user = await _userService.LoginAsync(dto.Username, dto.Password);
        if (user == null) {
            return Unauthorized("Invalid username or password");
        }

        var token = await _userService.CreateSessionAsync(user.Id);
        var refreshToken = await _userService.CreateRefreshTokenAsync(user.Id);

        SetRefreshTokenCookie(refreshToken);

        return Ok(new AuthResponseDto {
            Id = user.Id,
            Username = user.Username,
            Token = token
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh() {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken)) {
            return Unauthorized("No refresh token");
        }

        var newToken = await _userService.RefreshSessionAsync(refreshToken);
        if (newToken == null) {
            return Unauthorized("Invalid refresh token");
        }

        var userId = await _userService.GetUserIdByTokenAsync(newToken);
        if (userId == null) {
            return Unauthorized("User not found");
        }

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) {
            return Unauthorized("User not found");
        }

        var newRefreshToken = await _userService.CreateRefreshTokenAsync(user.Id);
        SetRefreshTokenCookie(newRefreshToken);

        return Ok(new { Token = newToken });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout() {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (!string.IsNullOrEmpty(token)) {
            await _userService.LogoutAsync(token);
        }

        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken)) {
            await _userService.RevokeRefreshTokenAsync(refreshToken);
        }

        Response.Cookies.Delete("refreshToken");
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe() {
        var user = CurrentUser;
        if (user == null) return Unauthorized();

        return Ok(new {
            user.Id,
            user.Username,
            user.Avatar,
            user.Bio,
            user.CreatedAt
        });
    }

    private void SetRefreshTokenCookie(string refreshToken) {
        var cookieOptions = new CookieOptions {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}