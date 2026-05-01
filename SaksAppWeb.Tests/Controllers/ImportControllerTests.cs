using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaksAppWeb.Controllers;
using SaksAppWeb.Services;
using System.Text;

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

    [Fact]
    public async Task Cases_Post_ValidatesFile()
    {
        var importer = new Mock<IHtmlCaseImporter>();
        var controller = new ImportController(importer.Object);

        var result = await controller.Cases(null!, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        importer.Verify(x => x.ImportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cases_Post_ReturnsError_WhenImportFails()
    {
        var importer = new Mock<IHtmlCaseImporter>();
        importer.Setup(x => x.ImportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImportResult.Fail("bad html"));

        var controller = new ImportController(importer.Object);
        var file = MakeFormFile("<html/>");

        var result = await controller.Cases(file, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Cases_Post_ReturnsResult_WhenImportSucceeds()
    {
        var importer = new Mock<IHtmlCaseImporter>();
        importer.Setup(x => x.ImportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImportResult.Ok(3, 1, 0, []));

        var controller = new ImportController(importer.Object);
        var file = MakeFormFile("<html/>");

        var result = await controller.Cases(file, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportResult>(view.Model);
        Assert.Equal(3, model.Created);
    }

    private static IFormFile MakeFormFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(bytes.Length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }
}
