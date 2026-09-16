using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TweetBackend.Services;

namespace TweetBackend.Middleware;

public class TokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
    private readonly UserService _userService;

    public TokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        UserService userService)
        : base(options, logger, encoder, clock) {
        _userService = userService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(token)) {
            token = Request.Query["access_token"];
        }

        if (string.IsNullOrWhiteSpace(token)) {
            return AuthenticateResult.NoResult();
        }

        var userId = await _userService.GetUserIdByTokenAsync(token);
        if (userId == null) {
            return AuthenticateResult.Fail("Invalid token");
        }

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) {
            return AuthenticateResult.Fail("User not found");
        }

        Request.HttpContext.Items["CurrentUser"] = user;
        Request.HttpContext.Items["UserId"] = user.Id;
        Request.HttpContext.Items["Username"] = user.Username;

        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("UserId", user.Id),
            new Claim("Username", user.Username)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}