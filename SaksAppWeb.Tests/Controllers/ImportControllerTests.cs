using Xunit;
using Microsoft.AspNetCore.Mvc;
using SaksAppWeb.Controllers;

namespace SaksAppWeb.Tests.Controllers;

public class ImportControllerTests
{
    [Fact]
    public void Cases_Get_ReturnsView()
    {
        var controller = new ImportController(null!);

        var result = controller.Cases();

        Assert.IsType<ViewResult>(result);
    }

    [Fact(Skip = "HtmlCaseImporter requires complex setup")]
    public async Task Cases_Post_ValidatesFile()
    {
    }

    [Fact(Skip = "HtmlCaseImporter requires complex setup")]
    public async Task Cases_Post_ReturnsError_WhenImportFails()
    {
    }

    [Fact(Skip = "HtmlCaseImporter requires complex setup")]
    public async Task Cases_Post_ReturnsResult_WhenImportSucceeds()
    {
    }
}