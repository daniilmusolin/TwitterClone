using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookmarksController : BaseController {
    private readonly TweetStore _store;

    public BookmarksController(TweetStore store) {
        _store = store;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetBookmarks() {
        if (CurrentUser == null) return Unauthorized();

        var bookmarks = await _store.GetBookmarksAsync(CurrentUserId!);
        return Ok(bookmarks);
    }

    [HttpPost("{postId}")]
    [Authorize]
    public async Task<IActionResult> AddBookmark(int postId) {
        if (CurrentUser == null) return Unauthorized();

        var success = await _store.AddBookmarkAsync(CurrentUserId!, postId);
        if (!success) return NotFound("Post not found");
        return Ok();
    }

    [HttpDelete("{postId}")]
    [Authorize]
    public async Task<IActionResult> RemoveBookmark(int postId) {
        if (CurrentUser == null) return Unauthorized();

        await _store.RemoveBookmarkAsync(CurrentUserId!, postId);
        return Ok();
    }
}