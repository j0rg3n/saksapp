using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaksAppWeb.Models;

namespace SaksAppWeb.Filters;

public class AdminOnlyAttribute : TypeFilterAttribute
{
    public AdminOnlyAttribute() : base(typeof(AdminOnlyFilter))
    {
    }
}

public class AdminOnlyFilter : IAsyncAuthorizationFilter
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminOnlyFilter(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = await _userManager.GetUserAsync(context.HttpContext.User);
        if (user == null || !user.IsAdmin)
        {
            context.Result = new ForbidResult();
        }
    }
}
