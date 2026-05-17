using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public class AppUserManager : UserManager<ApplicationUser>
{
    public AppUserManager(IUserStore<ApplicationUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<ApplicationUser>> logger)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators,
            keyNormalizer, errors, services, logger)
    {
    }

    public override async Task<IdentityResult> CreateAsync(ApplicationUser user)
    {
        var result = await base.CreateAsync(user);
        if (!result.Succeeded)
            return result;

        // Check if this is the first user
        var userCount = Users.Count();
        if (userCount == 1)
        {
            user.IsApproved = true;
            user.IsAdmin = true;
            var updateResult = await base.UpdateAsync(user);
            return updateResult;
        }

        return result;
    }
}
