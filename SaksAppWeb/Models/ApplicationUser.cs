using Microsoft.AspNetCore.Identity;

namespace SaksAppWeb.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsApproved { get; set; } = false;
    public bool IsAdmin { get; set; } = false;
}
