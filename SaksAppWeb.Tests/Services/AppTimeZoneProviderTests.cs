using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaksAppWeb.Services;
using Xunit;

namespace SaksAppWeb.Tests.Services;

public class AppTimeZoneProviderTests
{
    private static AppTimeZoneProvider Create(string? configured)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:TimeZone"] = configured })
            .Build();

        return new AppTimeZoneProvider(config, NullLogger<AppTimeZoneProvider>.Instance);
    }

    [Fact]
    public void DefaultsToOslo_WhenUnconfigured()
    {
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"), Create(null).Local);
    }

    [Fact]
    public void UsesConfiguredZone()
    {
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("UTC"), Create("UTC").Local);
    }

    /// <summary>A missing tzdata entry must degrade the window boundary, not stop startup.</summary>
    [Fact]
    public void FallsBackToUtc_WhenZoneUnknown()
    {
        Assert.Equal(TimeZoneInfo.Utc, Create("Mars/Olympus_Mons").Local);
    }
}
