using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class RequireApprovedUserMiddlewareTests
{
    private Mock<RequestDelegate> CreateNextDelegate()
    {
        var next = new Mock<RequestDelegate>();
        next.Setup(n => n(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        return next;
    }

    private Mock<ILogger<RequireApprovedUserMiddleware>> CreateLogger()
    {
        return new Mock<ILogger<RequireApprovedUserMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_AllowsAccess()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var userManager = new TestUserManager();

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Once);
    }

    // Helper: creates a ClaimsPrincipal that is actually authenticated (IsAuthenticated == true)
    private static ClaimsPrincipal AuthenticatedUser(string userId) =>
        new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
            authenticationType: "TestAuth"));

    [Fact]
    public async Task InvokeAsync_ApprovedUser_AllowsAccess()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = true };
        var httpContext = new DefaultHttpContext();
        httpContext.User = AuthenticatedUser("user-1");
        httpContext.Request.Path = "/Cases/Index";

        var userManager = new TestUserManager();
        userManager.AddUser(user);

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_UnapprovedUser_OnSecuredPath_DoesNotCallNext()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = false };
        var httpContext = new DefaultHttpContext();
        httpContext.User = AuthenticatedUser("user-1");
        httpContext.Request.Path = "/Cases/Index";

        var userManager = new TestUserManager();
        userManager.AddUser(user);

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_PendingApprovalPath_AllowsUnapprovedUser()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = false };
        var httpContext = new DefaultHttpContext();
        httpContext.User = AuthenticatedUser("user-1");
        httpContext.Request.Path = "/PendingApproval/Index";

        var userManager = new TestUserManager();
        userManager.AddUser(user);

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AccountPath_AllowsUnapprovedUser()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = false };
        var httpContext = new DefaultHttpContext();
        httpContext.User = AuthenticatedUser("user-1");
        httpContext.Request.Path = "/Account/Logout";

        var userManager = new TestUserManager();
        userManager.AddUser(user);

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CssPath_AllowsUnapprovedUser()
    {
        var next = CreateNextDelegate();
        var logger = CreateLogger();
        var middleware = new RequireApprovedUserMiddleware(next.Object, logger.Object);

        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = false };
        var httpContext = new DefaultHttpContext();
        httpContext.User = AuthenticatedUser("user-1");
        httpContext.Request.Path = "/css/site.css";

        var userManager = new TestUserManager();
        userManager.AddUser(user);

        await middleware.InvokeAsync(httpContext, userManager);

        next.Verify(n => n(httpContext), Times.Once);
    }
}
