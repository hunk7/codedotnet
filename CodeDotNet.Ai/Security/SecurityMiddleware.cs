using CodeDotNet.Ai.Configuration;

namespace CodeDotNet.Ai.Security;

public static class ApiErrors
{
    public static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);

    public static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { error = new { code, message } });
    }
}

/// <summary>
/// Host-header (anti DNS-rebinding), Origin allowlist, minimal CORS for allowed origins, and
/// bearer-token auth for everything except /v1/health and /v1/pair.
/// </summary>
public sealed class SecurityMiddleware(RequestDelegate next, AiOptions options, PairingService pairing, ILogger<SecurityMiddleware> logger)
{
    private static readonly string[] AnonymousPaths = ["/v1/health", "/v1/pair"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!OriginPolicy.IsHostAllowed(context.Request.Host.Value))
        {
            await ApiErrors.WriteAsync(context, StatusCodes.Status403Forbidden, "host_not_allowed", "Host header must be a loopback name.");
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (!OriginPolicy.IsOriginAllowed(origin, options.AllowedOrigins))
        {
            logger.LogWarning("Rejected request from disallowed Origin {Origin}", origin);
            await ApiErrors.WriteAsync(context, StatusCodes.Status403Forbidden, "origin_not_allowed", "Origin is not allowed.");
            return;
        }

        if (!string.IsNullOrEmpty(origin))
        {
            var headers = context.Response.Headers;
            headers.AccessControlAllowOrigin = origin;
            headers.Vary = "Origin";
            headers.AccessControlAllowHeaders = "Authorization, Content-Type";
            headers.AccessControlAllowMethods = "GET, POST, DELETE, OPTIONS";
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!AnonymousPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth["Bearer ".Length..].Trim() : null;
            if (!pairing.ValidateToken(token))
            {
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await ApiErrors.WriteAsync(context, StatusCodes.Status401Unauthorized, "unauthorized", "A valid session token is required. Pair via POST /v1/pair.");
                return;
            }
        }

        await next(context);
    }
}
