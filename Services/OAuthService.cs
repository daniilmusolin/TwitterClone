using System.Text.Json;
using System.Text;
using TweetBackend.DTOs;

namespace TweetBackend.Services;

public class OAuthService {
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(IConfiguration config, HttpClient httpClient, ILogger<OAuthService> logger) {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetVKAuthUrl(string redirectUri) {
        var clientId = _config["OAuth:VK:ClientId"];
        return $"https://oauth.vk.com/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&v=5.131&scope=email";
    }

    public string GetYandexAuthUrl(string redirectUri) {
        var clientId = _config["OAuth:Yandex:ClientId"];
        return $"https://oauth.yandex.ru/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code";
    }

    public async Task<OAuthUserInfo?> GetVKUserAsync(string code, string redirectUri) {
        try {
            var clientId = _config["OAuth:VK:ClientId"];
            var clientSecret = _config["OAuth:VK:ClientSecret"];

            var tokenUrl = $"https://oauth.vk.com/access_token?client_id={clientId}&client_secret={clientSecret}&redirect_uri={Uri.EscapeDataString(redirectUri)}&code={code}";
            var tokenResponse = await _httpClient.GetAsync(tokenUrl);

            if (!tokenResponse.IsSuccessStatusCode) {
                _logger.LogError("VK token request failed: {StatusCode}", tokenResponse.StatusCode);
                return null;
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenJson);

            if (tokenData == null || !tokenData.ContainsKey("access_token")) {
                return null;
            }

            var accessToken = tokenData["access_token"].ToString();
            var userId = tokenData["user_id"]?.ToString();

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId)) {
                return null;
            }

            var userUrl = $"https://api.vk.com/method/users.get?user_ids={userId}&fields=photo_100&access_token={accessToken}&v=5.131";
            var userResponse = await _httpClient.GetAsync(userUrl);

            if (!userResponse.IsSuccessStatusCode) {
                return null;
            }

            var userJson = await userResponse.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<VkUserResponse>(userJson);

            if (userData?.Response == null || userData.Response.Length == 0) {
                return null;
            }

            var vkUser = userData.Response[0];

            return new OAuthUserInfo {
                Id = $"vk_{userId}",
                Username = vkUser.FirstName + " " + vkUser.LastName,
                Email = tokenData.ContainsKey("email") ? tokenData["email"]?.ToString() : null,
                Avatar = vkUser.Photo100
            };
        } catch (Exception e) {
            _logger.LogError(e, "VK OAuth error");
            return null;
        }
    }

    public async Task<OAuthUserInfo?> GetYandexUserAsync(string code, string redirectUri) {
        try {
            var clientId = _config["OAuth:Yandex:ClientId"];
            var clientSecret = _config["OAuth:Yandex:ClientSecret"];

            var tokenData = new Dictionary<string, string> {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri
            };

            var tokenContent = new FormUrlEncodedContent(tokenData);
            var tokenResponse = await _httpClient.PostAsync("https://oauth.yandex.ru/token", tokenContent);

            if (!tokenResponse.IsSuccessStatusCode) {
                _logger.LogError("Yandex token request failed: {StatusCode}", tokenResponse.StatusCode);
                return null;
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenDataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenJson);

            if (tokenDataDict == null || !tokenDataDict.ContainsKey("access_token")) {
                return null;
            }

            var accessToken = tokenDataDict["access_token"].ToString();

            if (string.IsNullOrEmpty(accessToken)) {
                return null;
            }

            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info");
            userRequest.Headers.Add("Authorization", $"OAuth {accessToken}");

            var userResponse = await _httpClient.SendAsync(userRequest);

            if (!userResponse.IsSuccessStatusCode) {
                return null;
            }

            var userJson = await userResponse.Content.ReadAsStringAsync();
            var yandexUser = JsonSerializer.Deserialize<YandexUserResponse>(userJson);

            if (yandexUser == null) {
                return null;
            }

            return new OAuthUserInfo {
                Id = $"ya_{yandexUser.Id}",
                Username = yandexUser.DisplayName ?? yandexUser.Login,
                Email = yandexUser.DefaultEmail,
                Avatar = yandexUser.DefaultAvatarId != null
                    ? $"https://avatars.yandex.net/get-yapic/{yandexUser.DefaultAvatarId}/islands-50"
                    : null
            };
        } catch (Exception e) {
            _logger.LogError(e, "Yandex OAuth error");
            return null;
        }
    }
}