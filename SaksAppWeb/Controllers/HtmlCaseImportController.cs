using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaksAppWeb.Services;

namespace SaksAppWeb.Controllers;

[Authorize]
public class ImportController : Controller
{
    private readonly HtmlCaseImporter _importer;

    public ImportController(HtmlCaseImporter importer)
    {
        _importer = importer;
    }

    [HttpGet]
    public IActionResult Cases()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cases(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            ModelState.AddModelError("", "Please choose the exported Saker.html file.");
            return View();
        }

        string html;
        await using (var stream = file.OpenReadStream())
        using (var sr = new StreamReader(stream))
        {
            html = await sr.ReadToEndAsync(ct);
        }

        var result = await _importer.ImportAsync(html, ct);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Import failed.");
            return View();
        }

        return View(result);
    }
}
