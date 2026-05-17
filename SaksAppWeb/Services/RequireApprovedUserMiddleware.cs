using Microsoft.AspNetCore.Identity;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public class RequireApprovedUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequireApprovedUserMiddleware> _logger;

    public RequireApprovedUserMiddleware(RequestDelegate next, ILogger<RequireApprovedUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        // Skip middleware for unauthenticated users
        if (!context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Allow certain paths through
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/PendingApproval/") ||
            path.StartsWith("/Account/") ||
            path.StartsWith("/Identity/") ||
            path.StartsWith("/lib/") ||
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/images/"))
        {
            await _next(context);
            return;
        }

        // Get the current user
        var user = await userManager.GetUserAsync(context.User);
        if (user != null && !user.IsApproved)
        {
            context.Response.Redirect("/PendingApproval/Index");
            return;
        }

        await _next(context);
    }
}
