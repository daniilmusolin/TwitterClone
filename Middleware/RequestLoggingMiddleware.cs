namespace TweetBackend.Middleware;

public class RequestLoggingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger) {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context) {
        var userId = context.Items["UserId"] as string ?? "anonymous";
        var username = context.Items["Username"] as string ?? "anonymous";

        _logger.LogInformation(
            "Request: {Method} {Path} | User: {Username}({UserId})",
            context.Request.Method,
            context.Request.Path,
            username,
            userId
        );

        await _next(context);
    }
}