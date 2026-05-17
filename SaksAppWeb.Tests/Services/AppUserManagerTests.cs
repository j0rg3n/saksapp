using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

namespace SaksAppWeb.Tests.Services;

public class AppUserManagerTests
{
    private class TestableAppUserManager : AppUserManager
    {
        private readonly List<ApplicationUser> _users;

        public TestableAppUserManager(IUserStore<ApplicationUser> store, List<ApplicationUser> users)
            : base(store,
                new OptionsWrapper<IdentityOptions>(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                new Mock<ILogger<UserManager<ApplicationUser>>>().Object)
        {
            _users = users;
        }

        public override IQueryable<ApplicationUser> Users => _users.AsQueryable();

        public override Task<IdentityResult> UpdateAsync(ApplicationUser user)
        {
            var existingUser = _users.FirstOrDefault(u => u.Id == user.Id);
            if (existingUser != null)
            {
                var index = _users.IndexOf(existingUser);
                _users[index] = user;
            }
            return Task.FromResult(IdentityResult.Success);
        }
    }

    private (TestableAppUserManager manager, List<ApplicationUser> users, Mock<IUserStore<ApplicationUser>> storeMock) CreateManager()
    {
        var users = new List<ApplicationUser>();
        var storeMock = new Mock<IUserStore<ApplicationUser>>();

        // Mock CreateAsync to add the user to the list and return success
        storeMock.As<IUserStore<ApplicationUser>>()
            .Setup(s => s.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationUser, CancellationToken>((u, _) =>
            {
                u.Id = Guid.NewGuid().ToString();
                users.Add(u);
            })
            .ReturnsAsync(IdentityResult.Success);

        storeMock.As<IUserStore<ApplicationUser>>()
            .Setup(s => s.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var manager = new TestableAppUserManager(storeMock.Object, users);
        return (manager, users, storeMock);
    }

    [Fact]
    public async Task CreateAsync_FirstUser_GetsAutoApprovedAndAdmin()
    {
        var (manager, users, _) = CreateManager();
        var user = new ApplicationUser { UserName = "first@test.com", Email = "first@test.com" };

        var result = await manager.CreateAsync(user);

        Assert.True(result.Succeeded);
        Assert.True(user.IsApproved);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public async Task CreateAsync_SecondUser_IsNotAutoApprovedOrAdmin()
    {
        var (manager, users, _) = CreateManager();

        // Add first user to the list
        var firstUser = new ApplicationUser { Id = "user-1", UserName = "first@test.com", Email = "first@test.com" };
        users.Add(firstUser);

        var secondUser = new ApplicationUser { UserName = "second@test.com", Email = "second@test.com" };

        var result = await manager.CreateAsync(secondUser);

        Assert.True(result.Succeeded);
        Assert.False(secondUser.IsApproved);
        Assert.False(secondUser.IsAdmin);
    }
}
