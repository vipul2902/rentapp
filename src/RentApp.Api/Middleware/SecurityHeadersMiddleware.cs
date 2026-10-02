namespace RentApp.Api.Middleware;

/// <summary>
/// Defensive response headers for an API that returns personal and financial data:
/// - nothing is cached by browsers or proxies unless an endpoint says otherwise (no-store);
/// - responses cannot be sniffed as another content type, framed, or leak a referrer;
/// - a strict content security policy, since the API never serves pages (Swagger UI in Development excepted).
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        var isSwagger = environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger");
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            if (!isSwagger)
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}
