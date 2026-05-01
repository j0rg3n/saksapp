using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public interface IUserDisplayService
{
    Task<Dictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct);
}

public class UserDisplayService : IUserDisplayService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserDisplayService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var users = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.UserName })
            .ToListAsync(ct);

        return users.ToDictionary(
            x => x.Id,
            x => x.FullName ?? x.Email ?? x.UserName ?? x.Id);
    }
}