using Xunit;
using Microsoft.AspNetCore.Mvc;
using SaksAppWeb.Controllers;
using SaksAppWeb.Models;

namespace SaksAppWeb.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task Index_ReturnsViewWithUsers()
    {
        var userManager = new TestUserManager();
        var user = new ApplicationUser { Id = "user-1", UserName = "testuser", Email = "test@test.com" };
        userManager.AddUser(user);

        var controller = new UsersController(userManager);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<ApplicationUser>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenUserNotExists()
    {
        var userManager = new TestUserManager();
        var controller = new UsersController(userManager);

        var result = await controller.Edit("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact(Skip = "TestUserManager needs full implementation")]
    public async Task Edit_Get_ReturnsViewWithUser()
    {
    }

    [Fact(Skip = "TestUserManager needs full implementation")]
    public async Task Edit_Post_RedirectsToIndex_OnSuccess()
    {
    }
}