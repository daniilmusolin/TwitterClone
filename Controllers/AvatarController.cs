using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvatarController : BaseController {
    private readonly IWebHostEnvironment _env;
    private readonly UserService _userService;

    public AvatarController(IWebHostEnvironment env, UserService userService) {
        _env = env;
        _userService = userService;
    }

    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile file) {
        if (CurrentUser == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest("No file");

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest("Only images allowed (JPEG, PNG, GIF, WEBP)");

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("File too large (max 5MB)");

        try {
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{CurrentUser.Id}_{DateTime.Now.Ticks}{extension}";
            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "avatars");

            if (!Directory.Exists(uploadsPath)) {
                Directory.CreateDirectory(uploadsPath);
            }

            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(stream);
            }

            CurrentUser.Avatar = $"/uploads/avatars/{fileName}";

            return Ok(new {
                url = CurrentUser.Avatar,
                message = "Avatar uploaded successfully"
            });
        } catch (Exception ex) {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> DeleteAvatar() {
        if (CurrentUser == null) return Unauthorized();

        if (string.IsNullOrEmpty(CurrentUser.Avatar)) {
            return BadRequest("No avatar to delete");
        }

        try {
            var filePath = Path.Combine(_env.WebRootPath, CurrentUser.Avatar.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) {
                System.IO.File.Delete(filePath);
            }

            CurrentUser.Avatar = null;
            return Ok(new { message = "Avatar deleted" });
        } catch (Exception ex) {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}