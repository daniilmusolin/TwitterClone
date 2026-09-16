using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TweetBackend.Models;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase {
    protected User? CurrentUser => HttpContext.Items["CurrentUser"] as User;

    protected string? CurrentUserId {
        get {
            if (HttpContext.Items.TryGetValue("UserId", out var userId)) {
                return userId as string;
            }
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }

    protected string? CurrentUsername {
        get {
            if (HttpContext.Items.TryGetValue("Username", out var username)) {
                return username as string;
            }
            return User.FindFirstValue(ClaimTypes.Name);
        }
    }

    protected IActionResult Unauthorized(string message = "Unauthorized") {
        return base.Unauthorized(new { error = message });
    }

    protected IActionResult Forbidden(string message = "Forbidden") {
        return StatusCode(403, new { error = message });
    }
}