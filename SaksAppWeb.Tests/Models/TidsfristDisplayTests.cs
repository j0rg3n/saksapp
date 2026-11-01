using SaksAppWeb.Models;
using Xunit;

namespace SaksAppWeb.Tests.Models;

public class TidsfristDisplayTests
{
    [Fact]
    public void OverrideWinsOverCase_AndDateUsesDottedFormat() =>
        Assert.Equal("02.10.2026 eksempeltekst",
            TidsfristDisplay.Format(new DateOnly(2026, 10, 2), "eksempeltekst", new DateOnly(2027, 1, 1), "case"));

    [Fact]
    public void FallsBackToCaseTidsfrist() =>
        Assert.Equal("15.04.2026", TidsfristDisplay.Format(null, null, new DateOnly(2026, 4, 15), null));

    [Fact]
    public void TextOnly() =>
        Assert.Equal("Kun tekst", TidsfristDisplay.Format(null, "Kun tekst", null, null));

    [Fact]
    public void DefaultsToNextMeeting() =>
        Assert.Equal("Innen neste møte", TidsfristDisplay.Format(null, " ", null, null));
}
