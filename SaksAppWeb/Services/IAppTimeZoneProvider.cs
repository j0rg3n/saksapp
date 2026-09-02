using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SaksAppWeb.Services;

/// <summary>
/// The time zone meeting times are expressed in. Meeting times are wall-clock local, while
/// CaseEvent.CreatedAt is UTC, so anything comparing the two has to convert through this.
/// </summary>
public interface IAppTimeZoneProvider
{
    TimeZoneInfo Local { get; }
}

public sealed class AppTimeZoneProvider : IAppTimeZoneProvider
{
    public const string DefaultTimeZoneId = "Europe/Oslo";

    public TimeZoneInfo Local { get; }

    public AppTimeZoneProvider(IConfiguration configuration, ILogger<AppTimeZoneProvider> logger)
    {
        var id = configuration["App:TimeZone"];
        if (string.IsNullOrWhiteSpace(id)) id = DefaultTimeZoneId;

        try
        {
            Local = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex)
        {
            // Falling back rather than throwing: a missing tzdata entry should degrade the window
            // boundary by an hour or two, not stop the application from starting.
            logger.LogWarning(ex, "Time zone '{TimeZoneId}' could not be resolved; falling back to UTC.", id);
            Local = TimeZoneInfo.Utc;
        }
    }
}
