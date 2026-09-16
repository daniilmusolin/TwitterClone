using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using TweetBackend.Models;
using TweetBackend.DTOs;

namespace TweetBackend.Services;

public class UserService {
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, string> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _refreshTokens = new();
    private readonly ILogger<UserService>? _logger;

    public UserService(ILogger<UserService>? logger = null) {
        _logger = logger;
    }

    public async Task<bool> IsUserOnlineAsync(string userId) {
        if (!_users.TryGetValue(userId, out var user)) return false;
        return await Task.FromResult(user.IsOnline);
    }

    public async Task<DateTime> GetUserLastSeenAsync(string userId) {
        if (!_users.TryGetValue(userId, out var user)) return DateTime.UtcNow;
        return await Task.FromResult(user.LastSeen);
    }

    public async Task SetUserOnlineAsync(string userId) {
        if (_users.TryGetValue(userId, out var user)) {
            user.IsOnline = true;
            user.LastSeen = DateTime.UtcNow;
        }
        await Task.CompletedTask;
    }

    public async Task SetUserOfflineAsync(string userId) {
        if (_users.TryGetValue(userId, out var user)) {
            user.IsOnline = false;
            user.LastSeen = DateTime.UtcNow;
        }
        await Task.CompletedTask;
    }

    public async Task<User?> RegisterAsync(string username, string password) {
        if (_users.Values.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return null;

        var user = new User {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsOnline = false,
            LastSeen = DateTime.UtcNow
        };

        _users[user.Id] = user;
        _logger?.LogInformation($"User registered: {username} ({user.Id})");
        return await Task.FromResult(user);
    }

    public async Task<User?> LoginAsync(string username, string password) {
        var user = _users.Values.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (user == null) {
            _logger?.LogWarning($"Login failed: user not found - {username}");
            return null;
        }

        if (!VerifyPassword(password, user.PasswordHash)) {
            _logger?.LogWarning($"Login failed: invalid password - {username}");
            return null;
        }

        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;

        _logger?.LogInformation($"User logged in: {username} ({user.Id})");
        return await Task.FromResult(user);
    }

    public async Task<string> CreateSessionAsync(string userId) {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _sessions[token] = userId;
        _logger?.LogInformation($"Session created for user: {userId}");
        return await Task.FromResult(token);
    }

    public async Task<string> CreateRefreshTokenAsync(string userId) {
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _refreshTokens[refreshToken] = userId;
        return await Task.FromResult(refreshToken);
    }

    public async Task<string?> RefreshSessionAsync(string refreshToken) {
        if (!_refreshTokens.TryGetValue(refreshToken, out var userId))
            return null;

        _refreshTokens.TryRemove(refreshToken, out _);
        var newToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _sessions[newToken] = userId;

        _logger?.LogInformation($"Session refreshed for user: {userId}");
        return await Task.FromResult(newToken);
    }

    public async Task<User?> GetUserByTokenAsync(string token) {
        if (!_sessions.TryGetValue(token, out var userId)) return null;
        return await Task.FromResult(_users.TryGetValue(userId, out var user) ? user : null);
    }

    public async Task<string?> GetUserIdByTokenAsync(string token) {
        if (_sessions.TryGetValue(token, out var userId)) {
            return await Task.FromResult(userId);
        }
        return null;
    }

    public async Task<User?> GetUserByIdAsync(string id) {
        return await Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);
    }

    public async Task<User?> GetUserByUsernameAsync(string username) {
        return await Task.FromResult(_users.Values.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task LogoutAsync(string token) {
        if (_sessions.TryGetValue(token, out var userId)) {
            if (_users.TryGetValue(userId, out var user)) {
                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
            }
            _sessions.TryRemove(token, out _);
            _logger?.LogInformation($"User logged out: {userId}");
        }
        await Task.CompletedTask;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken) {
        _refreshTokens.TryRemove(refreshToken, out _);
        await Task.CompletedTask;
    }

    private string HashPassword(string password) {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private bool VerifyPassword(string password, string hash) {
        var hashed = HashPassword(password);
        return hashed == hash;
    }

    public async Task<bool> FollowAsync(string followerId, string followeeId) {
        var follower = await GetUserByIdAsync(followerId);
        var followee = await GetUserByIdAsync(followeeId);

        if (follower == null || followee == null) return false;
        if (follower.Following.Contains(followeeId)) return false;

        follower.Following.Add(followeeId);
        followee.Followers.Add(followerId);

        _logger?.LogInformation($"User {followerId} followed {followeeId}");
        return true;
    }

    public async Task<bool> UnfollowAsync(string followerId, string followeeId) {
        var follower = await GetUserByIdAsync(followerId);
        var followee = await GetUserByIdAsync(followeeId);

        if (follower == null || followee == null) return false;
        if (!follower.Following.Contains(followeeId)) return false;

        follower.Following.Remove(followeeId);
        followee.Followers.Remove(followerId);

        _logger?.LogInformation($"User {followerId} unfollowed {followeeId}");
        return true;
    }

    public async Task<List<User>> SearchUsersAsync(string query, int page = 1, int pageSize = 20) {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) {
            return new List<User>();
        }

        var allUsers = _users.Values.ToList();

        var filtered = allUsers
            .Where(u => u.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return await Task.FromResult(filtered);
    }

    public async Task<int> GetSearchTotalCountAsync(string query) {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) {
            return 0;
        }

        return await Task.FromResult(_users.Values
            .Count(u => u.Username.Contains(query, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<List<User>> GetAllUsersAsync() {
        return await Task.FromResult(_users.Values.ToList());
    }
}