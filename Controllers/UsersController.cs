using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TweetApp.DTOs;
using TweetBackend.Hubs;
using TweetBackend.Services;

namespace TweetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : BaseController {
    private readonly UserService _userService;
    private readonly TweetStore _store;
    private readonly IHubContext<NotificationHub> _hubContext;

    public UsersController(
        UserService userService,
        TweetStore store,
        IHubContext<NotificationHub> hubContext) {
        _userService = userService;
        _store = store;
        _hubContext = hubContext;
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> GetProfile(string username) {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null) return NotFound("User not found");

        var isFollowing = CurrentUser != null && user.Followers.Contains(CurrentUserId!);

        return Ok(new {
            user.Id,
            user.Username,
            user.Bio,
            user.Avatar,
            user.CreatedAt,
            user.IsOnline,
            user.LastSeen,
            Followers = user.Followers.Count,
            Following = user.Following.Count,
            IsFollowing = isFollowing,
            PostsCount = await _store.GetUserPostsCount(username)
        });
    }

    [HttpGet("{username}/status")]
    public async Task<IActionResult> GetUserStatus(string username) {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null) return NotFound("User not found");

        return Ok(new {
            user.IsOnline,
            user.LastSeen
        });
    }

    [HttpGet("{username}/followers")]
    public async Task<IActionResult> GetFollowers(string username) {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null) return NotFound();

        var followers = new List<object>();
        foreach (var followerId in user.Followers) {
            var follower = await _userService.GetUserByIdAsync(followerId);
            if (follower != null) {
                var isFollowing = CurrentUser != null && CurrentUser.Following.Contains(follower.Id);

                followers.Add(new {
                    follower.Id,
                    follower.Username,
                    follower.Avatar,
                    follower.IsOnline,
                    IsFollowing = isFollowing
                });
            }
        }
        return Ok(followers);
    }

    [HttpGet("{username}/following")]
    public async Task<IActionResult> GetFollowing(string username) {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null) return NotFound();

        var following = new List<object>();
        foreach (var followingId in user.Following) {
            var follow = await _userService.GetUserByIdAsync(followingId);
            if (follow != null) {
                var isFollowing = CurrentUser != null && CurrentUser.Following.Contains(follow.Id);

                following.Add(new {
                    follow.Id,
                    follow.Username,
                    follow.Avatar,
                    follow.IsOnline,
                    IsFollowing = isFollowing
                });
            }
        }
        return Ok(following);
    }

    [HttpPost("{username}/follow")]
    [Authorize]
    public async Task<IActionResult> Follow(string username) {
        if (CurrentUser == null) return Unauthorized();

        var targetUser = await _userService.GetUserByUsernameAsync(username);
        if (targetUser == null) return NotFound("User not found");

        if (CurrentUserId == targetUser.Id)
            return BadRequest("Cannot follow yourself");

        var success = await _userService.FollowAsync(CurrentUserId!, targetUser.Id);
        if (!success) return BadRequest("Already following");

        await _store.AddFollowNotificationAsync(targetUser.Id, CurrentUsername!);

        return Ok(new { success = true });
    }

    [HttpDelete("{username}/follow")]
    [Authorize]
    public async Task<IActionResult> Unfollow(string username) {
        if (CurrentUser == null) return Unauthorized();

        var targetUser = await _userService.GetUserByUsernameAsync(username);
        if (targetUser == null) return NotFound("User not found");

        var success = await _userService.UnfollowAsync(CurrentUserId!, targetUser.Id);
        if (!success) return BadRequest("Not following");

        await _store.RemoveFollowNotificationAsync(targetUser.Id, CurrentUsername!);

        return Ok(new { success = true });
    }

    [HttpGet("{username}/posts")]
    public async Task<IActionResult> GetUserPosts(string username) {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null) return NotFound("User not found");

        var posts = await _store.GetUserPostsAsync(username);
        return Ok(posts);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
        if (string.IsNullOrWhiteSpace(query)) {
            return Ok(new { users = new List<object>(), pagination = new { page, pageSize, total = 0, totalPages = 0 } });
        }

        var users = await _userService.SearchUsersAsync(query, page, pageSize);
        var total = await _userService.GetSearchTotalCountAsync(query);

        var result = users.Select(u => new {
            u.Id,
            u.Username,
            u.Avatar,
            u.IsOnline,
            u.Bio
        });

        return Ok(new {
            users = result,
            pagination = new {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            }
        });
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto) {
        if (CurrentUser == null) return Unauthorized();

        if (dto.Bio != null) {
            CurrentUser.Bio = dto.Bio;
        }
        if (dto.Avatar != null) {
            CurrentUser.Avatar = dto.Avatar;
        }

        return Ok(new {
            success = true,
            bio = CurrentUser.Bio,
            avatar = CurrentUser.Avatar
        });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllUsers() {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users.Select(u => new {
            u.Id,
            u.Username,
            u.Avatar,
            u.IsOnline
        }));
    }
}