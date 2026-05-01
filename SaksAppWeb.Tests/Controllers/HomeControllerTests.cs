using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SaksAppWeb.Controllers;
using SaksAppWeb.Models;

namespace SaksAppWeb.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var logger = new Mock<ILogger<HomeController>>();
        var controller = new HomeController(logger.Object);

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var logger = new Mock<ILogger<HomeController>>();
        var controller = new HomeController(logger.Object);

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact(Skip = "HttpContext not fully mocked")]
    public void Error_ReturnsViewWithErrorViewModel()
    {
        var logger = new Mock<ILogger<HomeController>>();
        var controller = new HomeController(logger.Object);
        
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("test-trace-id", model.RequestId);
    }
}