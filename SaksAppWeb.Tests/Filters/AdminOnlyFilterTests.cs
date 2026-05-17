using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using SaksAppWeb.Models;
using SaksAppWeb.Filters;

namespace SaksAppWeb.Tests.Filters;

public class AdminOnlyFilterTests
{
    private AuthorizationFilterContext CreateAuthorizationContext(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new(), new());
        var filterContext = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        return filterContext;
    }

    [Fact]
    public async Task OnAuthorizationAsync_NonAdminUser_ReturnsForbid()
    {
        var userManager = new TestUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsAdmin = false };
        userManager.AddUser(user);

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") });
        var principal = new ClaimsPrincipal(claims);
        var context = CreateAuthorizationContext(principal);

        // Mock GetUserAsync
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var filter = new AdminOnlyFilter(userManagerMock.Object);

        await filter.OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_AdminUser_AllowsAccess()
    {
        var userManager = new TestUserManager();
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin", Email = "admin@test.com", IsAdmin = true };
        userManager.AddUser(user);

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin-1") });
        var principal = new ClaimsPrincipal(claims);
        var context = CreateAuthorizationContext(principal);

        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var filter = new AdminOnlyFilter(userManagerMock.Object);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_UserNotFound_ReturnsForbid()
    {
        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "nonexistent") });
        var principal = new ClaimsPrincipal(claims);
        var context = CreateAuthorizationContext(principal);

        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser)null!);

        var filter = new AdminOnlyFilter(userManagerMock.Object);

        await filter.OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        Assert.IsType<ForbidResult>(context.Result);
    }
}
