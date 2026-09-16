using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TweetBackend.DTOs;
using TweetBackend.Hubs;
using TweetBackend.Models;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : BaseController {
    private readonly TweetStore _store;
    private readonly IHubContext<NotificationHub> _hubContext;

    public PostsController(TweetStore store, IHubContext<NotificationHub> hubContext) {
        _store = store;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeComments = false) {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var userId = CurrentUserId;
        var username = CurrentUsername;  

        var (posts, total) = await _store.GetAllPostsPaginatedAsync(
            userId,
            username,  
            page,
            pageSize,
            includeComments
        );

        return Ok(new {
            posts,
            pagination = new {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id) {
        var post = await _store.GetPostAsync(id, CurrentUserId);
        if (post == null) return NotFound();
        return Ok(post);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("CreatePost")]
    public async Task<IActionResult> Create([FromBody] PostCreateDto dto) {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var post = new Post {
            Content = dto.Content,
            Author = CurrentUsername ?? "Anonymous",
            AuthorId = CurrentUserId ?? string.Empty
        };

        var created = await _store.AddPostAsync(post);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] PostUpdateDto dto) {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var post = await _store.GetPostAsync(id);
        if (post == null) return NotFound();

        if (post.AuthorId != CurrentUserId) {
            return Forbidden("You can only edit your own posts");
        }

        var success = await _store.UpdatePostAsync(id, dto.Content);
        if (!success) return NotFound();

        await _hubContext.Clients.All.SendAsync("PostUpdated", new {
            postId = id,
            content = dto.Content,
            updatedAt = DateTime.UtcNow
        });

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id) {
        var post = await _store.GetPostAsync(id);
        if (post == null) return NotFound();

        if (post.AuthorId != CurrentUserId) {
            return Forbidden("You can only delete your own posts");
        }

        var success = await _store.DeletePostAsync(id);
        if (!success) return NotFound();

        await _hubContext.Clients.All.SendAsync("PostDeleted", new {
            postId = id,
            deletedAt = DateTime.UtcNow
        });

        return NoContent();
    }

    [HttpPost("{id}/like")]
    [Authorize]
    [EnableRateLimiting("LikePost")]
    public async Task<IActionResult> ToggleLike(int id) {
        if (string.IsNullOrWhiteSpace(CurrentUsername))
            return BadRequest("User identifier required");

        var isLiked = await _store.ToggleLikeAsync(id, CurrentUsername, CurrentUserId!);

        var post = await _store.GetPostAsync(id);
        var likesCount = post?.Likes.Count ?? 0;

        return Ok(new {
            isLiked = isLiked,       
            likesCount = likesCount  
        });
    }

    [HttpPost("{id}/repost")]
    [Authorize]
    public async Task<IActionResult> Repost(int id) {
        var post = await _store.GetPostAsync(id);
        if (post == null) return NotFound();

        var existingReposts = await _store.GetRepostsAsync(id);
        if (existingReposts.Any(r => r.UserId == CurrentUserId)) {
            return Ok(new {
                success = true,
                reposts = existingReposts.Count,
                isReposted = true
            });
        }

        var success = await _store.AddRepostAsync(id, CurrentUserId!, CurrentUsername!);
        if (!success) {
            return BadRequest(new { error = "Failed to repost" });
        }

        var updatedPost = await _store.GetPostAsync(id);
        var reposts = await _store.GetRepostsAsync(id);
        return Ok(new {
            success = true,
            reposts = reposts.Count,
            isReposted = true
        });
    }

    [HttpDelete("{id}/repost")]
    [Authorize]
    public async Task<IActionResult> Unrepost(int id) {
        var success = await _store.RemoveRepostAsync(id, CurrentUserId!);
        if (!success) {
            return BadRequest(new { error = "Not reposted" });
        }

        var reposts = await _store.GetRepostsAsync(id);
        return Ok(new {
            success = true,
            reposts = reposts.Count,
            isReposted = false
        });
    }

    [HttpGet("hashtag/{tag}")]
    public async Task<IActionResult> GetByHashtag(string tag) {
        var posts = await _store.GetPostsByHashtagAsync(tag);
        return Ok(posts);
    }
}