using Microsoft.AspNetCore.Identity;

namespace SaksAppWeb.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
