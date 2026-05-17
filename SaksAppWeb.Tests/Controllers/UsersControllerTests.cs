using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using SaksAppWeb.Controllers;
using SaksAppWeb.Models;

namespace SaksAppWeb.Tests.Controllers;

public class UsersControllerTests
{
    private TestUserManager CreateUserManager()
    {
        return new TestUserManager();
    }

    private Mock<IHttpContextAccessor> CreateHttpContextAccessor(string userId)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        var httpContext = new Mock<HttpContext>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }));
        httpContext.Setup(x => x.User).Returns(user);
        accessor.Setup(x => x.HttpContext).Returns(httpContext.Object);
        return accessor;
    }

    [Fact]
    public async Task Index_ReturnsViewWithUsers()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com" };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("user-1");

        var controller = new UsersController(userManager, httpContextAccessor.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<ApplicationUser>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenUserNotExists()
    {
        var userManager = CreateUserManager();
        var httpContextAccessor = CreateHttpContextAccessor("user-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Edit("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Approve_ReturnsNotFound_WhenUserNotExists()
    {
        var userManager = CreateUserManager();
        var httpContextAccessor = CreateHttpContextAccessor("admin-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Approve("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Approve_SetsIsApprovedTrue_AndRedirects()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = false };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("admin-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Approve("user-1");

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await userManager.FindByIdAsync("user-1");
        Assert.True(updated?.IsApproved);
    }

    [Fact]
    public async Task Unapprove_SetsIsApprovedFalse_AndRedirects()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", IsApproved = true };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("admin-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Unapprove("user-1");

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await userManager.FindByIdAsync("user-1");
        Assert.False(updated?.IsApproved);
    }

    [Fact]
    public async Task ToggleAdmin_PreventsSelfDemotion()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin", Email = "admin@test.com", IsAdmin = true };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("admin-1");

        var controller = new UsersController(userManager, httpContextAccessor.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin-1") }));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ToggleAdmin("admin-1");

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await userManager.FindByIdAsync("admin-1");
        Assert.True(updated?.IsAdmin); // Should still be admin
    }

    [Fact]
    public async Task ToggleAdmin_TogglesAdminStatus_ForOtherUser()
    {
        var userManager = CreateUserManager();
        var admin = new ApplicationUser { Id = "admin-1", UserName = "admin", Email = "admin@test.com", IsAdmin = true };
        var regularUser = new ApplicationUser { Id = "user-2", UserName = "user", Email = "user@test.com", IsAdmin = false };
        userManager.AddUser(admin);
        userManager.AddUser(regularUser);
        var httpContextAccessor = CreateHttpContextAccessor("admin-1");

        var controller = new UsersController(userManager, httpContextAccessor.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin-1") }));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ToggleAdmin("user-2");

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await userManager.FindByIdAsync("user-2");
        Assert.True(updated?.IsAdmin);
    }

    [Fact]
    public async Task Edit_Get_ReturnsViewWithUser()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", FullName = "Test User" };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("user-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Edit("user-1");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ApplicationUser>(viewResult.Model);
        Assert.Equal("user-1", model.Id);
    }

    [Fact]
    public async Task Edit_Post_RedirectsToIndex_OnSuccess()
    {
        var userManager = CreateUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com", FullName = null };
        userManager.AddUser(user);
        var httpContextAccessor = CreateHttpContextAccessor("user-1");
        var controller = new UsersController(userManager, httpContextAccessor.Object);

        var result = await controller.Edit("user-1", "Updated Name");

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await userManager.FindByIdAsync("user-1");
        Assert.Equal("Updated Name", updated?.FullName);
    }
}