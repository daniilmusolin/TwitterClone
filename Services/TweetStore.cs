using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TweetApp.DTOs;
using TweetBackend.DTOs;
using TweetBackend.Hubs;
using TweetBackend.Models;

namespace TweetBackend.Services;

public class TweetStore {
    private readonly ConcurrentDictionary<int, Post> _posts = new();
    private readonly ConcurrentDictionary<string, List<Notification>> _notifications = new();
    private readonly ConcurrentDictionary<int, HashSet<string>> _postViews = new();
    private readonly ConcurrentDictionary<string, HashSet<int>> _userBookmarks = new();
    private readonly ConcurrentDictionary<int, List<Repost>> _reposts = new();
    private readonly ConcurrentDictionary<string, HashSet<int>> _hashtags = new();

    private int _nextPostId = 1;
    private int _nextCommentId = 1;
    private int _nextNotificationId = 1;
    private int _nextRepostId = 1;

    private readonly ILogger<TweetStore>? _logger;
    private readonly IHubContext<NotificationHub>? _hubContext;
    private readonly UserService? _userService;

    public TweetStore(ILogger<TweetStore>? logger = null, IHubContext<NotificationHub>? hubContext = null, UserService? userService = null) {
        _logger = logger;
        _hubContext = hubContext;
        _userService = userService;
    }

    public async Task<Post> AddPostAsync(Post post) {
        post.Id = Interlocked.Increment(ref _nextPostId);
        post.Views = 0;
        post.Reposts = new List<Repost>();
        _posts[post.Id] = post;

        await ProcessMentions(post.Content, post.Author, post.Id);
        ProcessHashtags(post.Content, post.Id);

        _logger?.LogInformation($"Post created: {post.Id} by {post.Author}");
        return post;
    }

    public async Task<Post?> GetPostAsync(int id, string? userId = null) {
        if (!_posts.TryGetValue(id, out var post)) return null;

        if (!string.IsNullOrEmpty(userId) && userId != post.AuthorId) {
            IncrementView(id, userId);
        }

        post.Views = GetViewsCount(id);
        post.Reposts = GetReposts(id);
        return post;
    }

    public async Task<(IEnumerable<PostDto> Posts, int Total)> GetAllPostsPaginatedAsync(
        string? userId = null,
        string? username = null,
        int page = 1,
        int pageSize = 20,
        bool includeComments = false) {

        var posts = _posts.Values
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var total = posts.Count;

        var paged = posts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new List<PostDto>();

        foreach (var post in paged) {
            if (!string.IsNullOrEmpty(userId) && userId != post.AuthorId) {
                IncrementView(post.Id, userId);
            }

            var repostList = _reposts.TryGetValue(post.Id, out var r) ? r : new List<Repost>();
            var isLiked = post.Likes.Contains(username ?? string.Empty);
            var isReposted = !string.IsNullOrEmpty(userId) && repostList.Any(r => r.UserId == userId);

            var postDto = new PostDto {
                Id = post.Id,
                Content = post.Content,
                Author = post.Author,
                AuthorId = post.AuthorId,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                LikesCount = post.Likes.Count,
                CommentsCount = CountAllComments(post.Comments),
                RepostsCount = repostList.Count,
                Views = GetViewsCount(post.Id),
                IsLiked = isLiked,
                IsReposted = isReposted
            };

            result.Add(postDto);
        }

        return await Task.FromResult((result.AsEnumerable(), total));
    }

    public async Task<bool> UpdatePostAsync(int id, string newContent) {
        if (!_posts.TryGetValue(id, out var post)) return false;
        post.Content = newContent;
        post.UpdatedAt = DateTime.UtcNow;
        ProcessHashtags(newContent, id);
        return true;
    }

    public async Task<bool> DeletePostAsync(int id) {
        var result = _posts.TryRemove(id, out _);
        if (result) {
            _logger?.LogInformation($"Post deleted: {id}");
        }
        return result;
    }

    public async Task<bool> ToggleLikeAsync(int postId, string username, string userId) {
        if (!_posts.TryGetValue(postId, out var post)) return false;

        if (post.Likes.Contains(username)) {
            post.Likes.Remove(username);
            await RemoveLikeNotificationAsync(postId, username);
            return false;
        } else {
            post.Likes.Add(username);
            await AddLikeNotificationAsync(postId, username, userId, post.Author);
            return true;  
        }
    }

    public async Task<bool> AddRepostAsync(int postId, string userId, string username) {
        if (!_posts.ContainsKey(postId)) return false;

        if (_reposts.TryGetValue(postId, out var existing) && existing.Any(r => r.UserId == userId)) {
            return false;
        }

        var repost = new Repost {
            Id = Interlocked.Increment(ref _nextRepostId),
            PostId = postId,
            UserId = userId,
            Username = username,
            CreatedAt = DateTime.UtcNow
        };

        if (!_reposts.ContainsKey(postId)) {
            _reposts[postId] = new List<Repost>();
        }

        _reposts[postId].Add(repost);

        if (_posts.TryGetValue(postId, out var post)) {
            post.Reposts = _reposts[postId].ToList();
        }

        _logger?.LogInformation($"Repost added: {postId} by {username}");
        return true;
    }

    public async Task<bool> RemoveRepostAsync(int postId, string userId) {
        if (!_reposts.TryGetValue(postId, out var list)) return false;
        var repost = list.FirstOrDefault(r => r.UserId == userId);
        if (repost == null) return false;
        var result = list.Remove(repost);
        if (result) {
            if (_posts.TryGetValue(postId, out var post)) {
                post.Reposts = _reposts.TryGetValue(postId, out var newList) ? newList.ToList() : new List<Repost>();
            }
            _logger?.LogInformation($"Repost removed: {postId} by {userId}");
        }
        return result;
    }

    public async Task<List<Repost>> GetRepostsAsync(int postId) {
        return _reposts.TryGetValue(postId, out var list) ? list.ToList() : new List<Repost>();
    }

    private List<Repost> GetReposts(int postId) {
        return _reposts.TryGetValue(postId, out var list) ? list.ToList() : new List<Repost>();
    }

    private void ProcessHashtags(string content, int postId) {
        var hashtags = Regex.Matches(content, @"#(\w+)");

        foreach (Match match in hashtags) {
            var tag = match.Groups[1].Value.ToLower();
            if (!_hashtags.ContainsKey(tag)) {
                _hashtags[tag] = new HashSet<int>();
            }
            _hashtags[tag].Add(postId);
        }
    }

    public async Task<List<Post>> GetPostsByHashtagAsync(string tag) {
        var tagLower = tag.ToLower();
        if (!_hashtags.TryGetValue(tagLower, out var postIds)) {
            return new List<Post>();
        }

        return postIds
            .Where(id => _posts.ContainsKey(id))
            .Select(id => _posts[id])
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    private async Task ProcessMentions(string content, string fromUser, int? postId = null, int? commentId = null) {
        var mentions = Regex.Matches(content, @"@(\w+)");

        foreach (Match match in mentions) {
            var username = match.Groups[1].Value;
            if (_userService == null) continue;

            var user = await _userService.GetUserByUsernameAsync(username);
            if (user != null && user.Username != fromUser) {
                await AddNotificationAsync(new Notification {
                    UserId = user.Id,
                    Type = "mention",
                    PostId = postId?.ToString(),
                    CommentId = commentId?.ToString(),
                    FromUser = fromUser,
                    Message = content,
                    IsRead = false
                });
            }
        }
    }

    public async Task<Comment?> AddCommentAsync(
        int postId,
        Comment comment,
        int? parentCommentId = null,
        string? excludeConnectionId = null) {

        if (!_posts.TryGetValue(postId, out var post)) {
            _logger?.LogWarning($"Post {postId} not found");
            return null;
        }

        comment.Id = Interlocked.Increment(ref _nextCommentId);
        comment.PostId = postId;
        comment.ParentCommentId = parentCommentId;
        comment.CreatedAt = DateTime.UtcNow;

        _logger?.LogInformation($"Adding comment: Id={comment.Id}, PostId={postId}, ParentId={parentCommentId}");

        if (parentCommentId.HasValue) {
            var parent = FindComment(post.Comments, parentCommentId.Value);
            if (parent != null) {
                if (parent.Replies == null) parent.Replies = new List<Comment>();
                parent.Replies.Add(comment);
                _logger?.LogInformation($"Reply added: {comment.Id} to parent: {parentCommentId}");
            } else {
                _logger?.LogWarning($"Parent comment {parentCommentId} not found, adding as root");
                post.Comments.Add(comment);
            }
        } else {
            post.Comments.Add(comment);
            _logger?.LogInformation($"Root comment added: {comment.Id}");
        }
        var flat = new List<Comment>();
        FlattenComments(post.Comments, flat);
        _logger?.LogInformation($"Post {postId} now has {flat.Count} total comments");

        return comment;
    }

    public async Task<List<Comment>> GetCommentsAsync(int postId) {
        if (!_posts.TryGetValue(postId, out var post)) return new List<Comment>();

        var result = new List<Comment>();
        FlattenComments(post.Comments, result);

        _logger?.LogInformation($"GetComments: {result.Count} total comments for post {postId}");
        foreach (var c in result) {
            _logger?.LogInformation($"Comment {c.Id}: ParentId={c.ParentCommentId}, Replies={c.Replies?.Count ?? 0}");
        }

        return result;
    }

    private void FlattenComments(List<Comment> comments, List<Comment> result) {
        if (comments == null) {
            _logger?.LogWarning("FlattenComments: comments is null");
            return;
        }

        _logger?.LogInformation($"FlattenComments: processing {comments.Count} comments");

        foreach (var c in comments) {
            _logger?.LogInformation($"FlattenComments: adding comment {c.Id} (ParentId={c.ParentCommentId})");
            result.Add(c);
            if (c.Replies != null && c.Replies.Count > 0) {
                _logger?.LogInformation($"FlattenComments: comment {c.Id} has {c.Replies.Count} replies");
                FlattenComments(c.Replies, result);
            }
        }
    }

    private int CountAllComments(List<Comment> comments) {
        if (comments == null) return 0;
        int count = comments.Count;
        foreach (var c in comments) {
            if (c.Replies != null) {
                count += CountAllComments(c.Replies);
            }
        }
        return count;
    }

    public async Task<bool> DeleteCommentAsync(int postId, int commentId, string? excludeConnectionId = null) {
        if (!_posts.TryGetValue(postId, out var post)) return false;

        var deletedIds = new List<int>();
        var removed = RemoveCommentRecursive(post.Comments, commentId, deletedIds);

        if (removed && _hubContext != null) {
            await _hubContext.Clients.GroupExcept(
                $"comments_{postId}",
                new[] { excludeConnectionId ?? "" }
            ).SendAsync("DeleteComment", new {
                postId = postId,
                commentId = commentId,
                deletedIds = deletedIds
            });
        }

        return removed;
    }

    private bool RemoveCommentRecursive(List<Comment> comments, int commentId, List<int> deletedIds) {
        for (int i = comments.Count - 1; i >= 0; i--) {
            var comment = comments[i];

            if (comment.Id == commentId) {
                CollectAllIds(comment, deletedIds);
                deletedIds.Add(commentId);
                comments.RemoveAt(i);
                return true;
            }

            if (comment.Replies != null && comment.Replies.Count > 0) {
                if (RemoveCommentRecursive(comment.Replies, commentId, deletedIds)) {
                    return true;
                }
            }
        }
        return false;
    }

    private void CollectAllIds(Comment comment, List<int> ids) {
        if (comment.Replies == null) return;
        foreach (var reply in comment.Replies) {
            ids.Add(reply.Id);
            CollectAllIds(reply, ids);
        }
    }

    public async Task<bool> UpdateCommentAsync(int postId, int commentId, string newContent, string? excludeConnectionId = null) {
        if (!_posts.TryGetValue(postId, out var post)) return false;
        var comment = FindComment(post.Comments, commentId);
        if (comment == null) return false;

        comment.Content = newContent;
        comment.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private Comment? FindComment(List<Comment> comments, int id) {
        if (comments == null) return null;
        foreach (var c in comments) {
            if (c.Id == id) return c;
            if (c.Replies != null && c.Replies.Count > 0) {
                var found = FindComment(c.Replies, id);
                if (found != null) return found;
            }
        }
        return null;
    }

    public async Task AddNotificationAsync(Notification notification, Comment? relatedComment = null) {
        notification.Id = Interlocked.Increment(ref _nextNotificationId);
        notification.CreatedAt = DateTime.UtcNow;

        if (relatedComment != null && string.IsNullOrEmpty(notification.Message)) {
            notification.Message = relatedComment.Content;
        }

        if (!_notifications.ContainsKey(notification.UserId)) {
            _notifications[notification.UserId] = new List<Notification>();
        }

        var list = _notifications[notification.UserId];
        list.Insert(0, notification);

        while (list.Count > 100) {
            list.RemoveAt(list.Count - 1);
        }

        if (_hubContext != null) {
            await SendNotificationUpdateAsync(notification, relatedComment);
            await SendNotificationCountAsync(notification.UserId);
        }
    }

    public async Task AddFollowNotificationAsync(string userId, string fromUser) {
        var notification = new Notification {
            Id = Interlocked.Increment(ref _nextNotificationId),
            UserId = userId,
            Type = "follow",
            FromUser = fromUser,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        if (!_notifications.ContainsKey(userId)) {
            _notifications[userId] = new List<Notification>();
        }
        _notifications[userId].Insert(0, notification);

        if (_hubContext != null) {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", new {
                    notification.Id,
                    notification.Type,
                    notification.FromUser,
                    notification.CreatedAt,
                    IsRead = notification.IsRead
                });

            await SendNotificationCountAsync(userId);
        }
    }

    public async Task RemoveFollowNotificationAsync(string userId, string fromUser) {
        if (_notifications.TryGetValue(userId, out var list)) {
            var toRemove = list.Where(n => n.Type == "follow" && n.FromUser == fromUser).ToList();
            foreach (var n in toRemove) {
                list.Remove(n);
            }

            if (_hubContext != null) {
                await _hubContext.Clients.Group($"user_{userId}")
                    .SendAsync("RemoveNotification", new {
                        fromUser = fromUser,
                        type = "follow"
                    });

                await SendNotificationCountAsync(userId);
            }
        }
    }

    private async Task AddLikeNotificationAsync(int postId, string fromUser, string fromUserId, string postAuthor) {
        if (_userService == null) return;

        var author = await _userService.GetUserByUsernameAsync(postAuthor);
        if (author == null || author.Username == fromUser) return;

        await AddNotificationAsync(new Notification {
            UserId = author.Id,
            Type = "like",
            PostId = postId.ToString(),
            FromUser = fromUser,
            IsRead = false
        });
    }

    private async Task RemoveLikeNotificationAsync(int postId, string userIdentifier) {
        foreach (var userId in _notifications.Keys.ToList()) {
            var list = _notifications[userId];
            var toRemove = list.Where(n =>
                n.Type == "like" &&
                n.PostId == postId.ToString() &&
                n.FromUser == userIdentifier).ToList();

            foreach (var n in toRemove) {
                list.Remove(n);
                if (_hubContext != null) {
                    await _hubContext.Clients.Group($"user_{userId}")
                        .SendAsync("RemoveNotification", new {
                            postId = postId,
                            fromUser = userIdentifier,
                            type = "like"
                        });

                    await SendNotificationCountAsync(userId);
                }
            }
        }
    }

    private async Task SendNotificationUpdateAsync(Notification notification, Comment? relatedComment = null) {
        if (_hubContext == null) return;

        object? commentDto = null;
        if (relatedComment != null) {
            commentDto = new {
                Id = relatedComment.Id,
                Content = relatedComment.Content,
                Author = relatedComment.Author,
                CreatedAt = relatedComment.CreatedAt,
                ParentCommentId = relatedComment.ParentCommentId,
                ReplyToAuthor = relatedComment.ReplyToAuthor
            };
        }

        object dto = notification.Type switch {
            "comment" => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                PostId = int.Parse(notification.PostId ?? "0"),
                CommentId = notification.CommentId != null ? int.Parse(notification.CommentId) : (int?)null,
                Comment = commentDto,
                IsRead = notification.IsRead
            },
            "reply" => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                PostId = int.Parse(notification.PostId ?? "0"),
                CommentId = notification.CommentId != null ? int.Parse(notification.CommentId) : (int?)null,
                Comment = commentDto,
                IsRead = notification.IsRead
            },
            "like" => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                PostId = int.Parse(notification.PostId ?? "0"),
                IsRead = notification.IsRead
            },
            "follow" => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                IsRead = notification.IsRead
            },
            "mention" => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                notification.Message,
                PostId = notification.PostId != null ? int.Parse(notification.PostId) : (int?)null,
                CommentId = notification.CommentId != null ? int.Parse(notification.CommentId) : (int?)null,
                IsRead = notification.IsRead
            },
            _ => new {
                notification.Id,
                notification.Type,
                notification.FromUser,
                notification.CreatedAt,
                notification.Message,
                IsRead = notification.IsRead
            }
        };

        await _hubContext.Clients.Group($"user_{notification.UserId}")
            .SendAsync("ReceiveNotification", dto);
    }

    private async Task SendNotificationCountAsync(string userId) {
        if (_hubContext == null) return;

        var count = await GetUnreadCountAsync(userId);
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("NotificationCount", new { count = count });
    }

    public async Task<List<Notification>> GetNotificationsAsync(string userId) {
        if (!_notifications.TryGetValue(userId, out var list))
            return new List<Notification>();
        return list.OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string userId) {
        if (!_notifications.TryGetValue(userId, out var list))
            return 0;
        return list.Count(n => !n.IsRead);
    }

    public async Task MarkNotificationAsReadAsync(int notificationId) {
        foreach (var kv in _notifications) {
            var notif = kv.Value.FirstOrDefault(n => n.Id == notificationId);
            if (notif != null) {
                notif.IsRead = true;
                if (_hubContext != null) {
                    await SendNotificationCountAsync(kv.Key);
                }
                break;
            }
        }
        await Task.CompletedTask;
    }

    public async Task MarkAllAsReadAsync(string userId) {
        if (!_notifications.TryGetValue(userId, out var list)) return;
        foreach (var n in list) n.IsRead = true;

        if (_hubContext != null) {
            await SendNotificationCountAsync(userId);
        }
        await Task.CompletedTask;
    }

    public async Task DeleteNotificationAsync(string userId, int notificationId) {
        if (_notifications.TryGetValue(userId, out var list)) {
            var item = list.FirstOrDefault(n => n.Id == notificationId);
            if (item != null) {
                list.Remove(item);
                if (_hubContext != null) {
                    await SendNotificationCountAsync(userId);
                }
            }
        }
        await Task.CompletedTask;
    }

    public async Task DeleteAllNotificationsAsync(string userId) {
        if (_notifications.ContainsKey(userId)) {
            _notifications[userId].Clear();
            if (_hubContext != null) {
                await SendNotificationCountAsync(userId);
            }
        }
        await Task.CompletedTask;
    }

    public async Task<List<BookmarkDto>> GetBookmarksAsync(string userId) {
        if (!_userBookmarks.TryGetValue(userId, out var bookmarks))
            return new List<BookmarkDto>();

        return bookmarks.Select(id => new BookmarkDto { PostId = id }).ToList();
    }

    public async Task<bool> AddBookmarkAsync(string userId, int postId) {
        if (!_posts.ContainsKey(postId)) return false;

        if (!_userBookmarks.ContainsKey(userId))
            _userBookmarks[userId] = new HashSet<int>();

        _userBookmarks[userId].Add(postId);
        return true;
    }

    public async Task RemoveBookmarkAsync(string userId, int postId) {
        if (_userBookmarks.TryGetValue(userId, out var bookmarks)) {
            bookmarks.Remove(postId);
        }
        await Task.CompletedTask;
    }

    public async Task<int> GetUserPostsCount(string username) {
        return _posts.Values.Count(p => p.Author == username);
    }

    public async Task<List<Post>> GetUserPostsAsync(string username) {
        return _posts.Values
            .Where(p => p.Author == username)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    private int GetViewsCount(int postId) {
        if (_postViews.TryGetValue(postId, out var users)) {
            return users.Count;
        }
        return 0;
    }

    private void IncrementView(int postId, string userId) {
        if (!_postViews.ContainsKey(postId)) {
            _postViews[postId] = new HashSet<string>();
        }
        _postViews[postId].Add(userId);
    }
}