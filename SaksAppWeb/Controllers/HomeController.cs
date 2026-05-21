using System.Diagnostics;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Html;
using SaksAppWeb.Models;

namespace SaksAppWeb.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWebHostEnvironment _env;

    public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public IActionResult Index()
    {
        var mdPath = Path.Combine(_env.ContentRootPath, "Content", "home.md");
        var markdown = System.IO.File.Exists(mdPath)
            ? System.IO.File.ReadAllText(mdPath)
            : "# Velkommen";
        var html = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        return View((object)html);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}