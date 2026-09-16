namespace TweetBackend.DTOs;

public class OAuthLoginDto {
    public string Provider { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}

public class OAuthUserInfo {
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Avatar { get; set; }
}

public class VkUserResponse {
    public VkUser[] Response { get; set; } = Array.Empty<VkUser>();
}

public class VkUser {
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Photo100 { get; set; } = string.Empty;
}

public class YandexUserResponse {
    public string Id { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DefaultEmail { get; set; } = string.Empty;
    public string DefaultAvatarId { get; set; } = string.Empty;
}