using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BioGuard.Api.Config;

public static class MiddlewareExtensions
{
    public static void UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' wss: ws:; frame-ancestors 'none'");
            context.Response.Headers.Remove("X-Powered-By");
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("x-aspnet-version");
            await next();
        });
    }

    public static void UseGlobalExceptionHandler(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Unhandled exception: {Method} {Path}", context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal server error",
                    message = env.IsDevelopment() ? ex.Message : "An error occurred",
                    traceId = context.TraceIdentifier
                });
            }
        });
    }
}
