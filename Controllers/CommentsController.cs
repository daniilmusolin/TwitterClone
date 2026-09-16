using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TweetApp.DTOs;
using TweetBackend.DTOs;
using TweetBackend.Hubs;
using TweetBackend.Models;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/posts/{postId}/[controller]")]
public class CommentsController : BaseController {
    private readonly TweetStore _store;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(TweetStore store, IHubContext<NotificationHub> hubContext, ILogger<CommentsController> logger) {
        _store = store;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(int postId) {
        var comments = await _store.GetCommentsAsync(postId);
        _logger?.LogInformation($"Returning {comments.Count} comments for post {postId}");
        return Ok(comments);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("CreateComment")]
    public async Task<IActionResult> CreateComment(int postId, [FromBody] CommentCreateDto dto) {
        var post = await _store.GetPostAsync(postId);
        if (post == null) {
            return NotFound(new { error = "Post has been deleted", postDeleted = true });
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var comment = new Comment {
            Content = dto.Content,
            Author = CurrentUsername ?? "Anonymous",
            AuthorId = CurrentUserId ?? string.Empty,
            ParentCommentId = dto.ParentCommentId,
            ReplyToAuthor = dto.ReplyToAuthor
        };

        var excludeConnectionId = Request.Headers.TryGetValue("X-Connection-Id", out var connectionIdHeader)
            ? connectionIdHeader.ToString()
            : null;

        var created = await _store.AddCommentAsync(postId, comment, dto.ParentCommentId, excludeConnectionId);

        if (created == null) {
            return NotFound("Post not found");
        }

        await _hubContext.Clients.GroupExcept(
            $"comments_{postId}",
            new[] { excludeConnectionId ?? "" }
        ).SendAsync("NewComment", new {
            postId = postId,
            comment = created,
            level = dto.ParentCommentId.HasValue ? 1 : 0
        });

        var notifiedUsers = new HashSet<string>();

        if (dto.ParentCommentId.HasValue) {
            var parentComment = FindComment(post.Comments, dto.ParentCommentId.Value);

            if (parentComment != null && parentComment.AuthorId != CurrentUserId) {
                if (!notifiedUsers.Contains(parentComment.AuthorId)) {
                    notifiedUsers.Add(parentComment.AuthorId);

                    var notification = new Notification {
                        UserId = parentComment.AuthorId,
                        Type = "reply",
                        FromUser = CurrentUsername,
                        PostId = postId.ToString(),
                        CommentId = created.Id.ToString(),
                        Message = created.Content,
                        IsRead = false
                    };

                    await _store.AddNotificationAsync(notification, created);
                }
            }

            if (post.AuthorId != CurrentUserId && !notifiedUsers.Contains(post.AuthorId)) {
                notifiedUsers.Add(post.AuthorId);

                var notification = new Notification {
                    UserId = post.AuthorId,
                    Type = "comment",
                    FromUser = CurrentUsername,
                    PostId = postId.ToString(),
                    CommentId = created.Id.ToString(),
                    Message = created.Content,
                    IsRead = false
                };

                await _store.AddNotificationAsync(notification, created);
            }
        } else {
            if (post.AuthorId != CurrentUserId) {
                var notification = new Notification {
                    UserId = post.AuthorId,
                    Type = "comment",
                    FromUser = CurrentUsername,
                    PostId = postId.ToString(),
                    CommentId = created.Id.ToString(),
                    Message = created.Content,
                    IsRead = false
                };

                await _store.AddNotificationAsync(notification, created);
            }
        }

        return CreatedAtAction(nameof(GetComments), new { postId }, created);
    }

    [HttpPut("{commentId}")]
    [Authorize]
    public async Task<IActionResult> UpdateComment(int postId, int commentId, [FromBody] CommentUpdateDto dto) {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var post = await _store.GetPostAsync(postId);
        if (post == null) return NotFound("Post not found");

        var comment = FindComment(post.Comments, commentId);
        if (comment == null) return NotFound("Comment not found");

        if (comment.AuthorId != CurrentUserId) {
            return Forbidden("You can only edit your own comments");
        }

        var excludeConnectionId = Request.Headers.TryGetValue("X-Connection-Id", out var connectionIdHeader)
            ? connectionIdHeader.ToString()
            : null;

        var success = await _store.UpdateCommentAsync(postId, commentId, dto.Content, excludeConnectionId);
        if (!success) return NotFound();

        await _hubContext.Clients.GroupExcept(
            $"comments_{postId}",
            new[] { excludeConnectionId ?? "" }
        ).SendAsync("UpdateComment", new {
            postId = postId,
            commentId = commentId,
            content = dto.Content,
            updatedAt = DateTime.UtcNow
        });

        return NoContent();
    }

    [HttpDelete("{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int postId, int commentId) {
        var post = await _store.GetPostAsync(postId);
        if (post == null) return NotFound("Post not found");

        var comment = FindComment(post.Comments, commentId);
        if (comment == null) return NotFound("Comment not found");

        if (comment.AuthorId != CurrentUserId) {
            return Forbidden("You can only delete your own comments");
        }

        var excludeConnectionId = Request.Headers.TryGetValue("X-Connection-Id", out var connectionIdHeader)
            ? connectionIdHeader.ToString()
            : null;

        var success = await _store.DeleteCommentAsync(postId, commentId, excludeConnectionId);
        if (!success) return NotFound();

        await _hubContext.Clients.GroupExcept(
            $"comments_{postId}",
            new[] { excludeConnectionId ?? "" }
        ).SendAsync("DeleteComment", new {
            postId = postId,
            commentId = commentId
        });

        return Ok();
    }

    private Comment? FindComment(List<Comment> comments, int id) {
        foreach (var c in comments) {
            if (c.Id == id) return c;
            var found = FindComment(c.Replies, id);
            if (found != null) return found;
        }
        return null;
    }
}