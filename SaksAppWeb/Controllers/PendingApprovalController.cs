using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaksAppWeb.Controllers;

[Authorize]
public class PendingApprovalController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
