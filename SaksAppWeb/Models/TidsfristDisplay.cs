namespace SaksAppWeb.Models;

/// <summary>
/// Shared rendering of an agenda item's tidsfrist. The meeting-level override wins over the case's
/// own tidsfrist; with neither set the deadline is the next meeting. Dates use dd.MM.yyyy, like
/// <see cref="MeetingDisplay"/> — the default DateOnly.ToString() printed 02/10/2026.
/// </summary>
public static class TidsfristDisplay
{
    public const string Default = "Innen neste møte";

    public static string Format(DateOnly? overrideDate, string? overrideText, DateOnly? caseDate, string? caseText)
    {
        if (overrideDate is not null || !string.IsNullOrWhiteSpace(overrideText))
            return Join(overrideDate, overrideText);
        if (caseDate is not null || !string.IsNullOrWhiteSpace(caseText))
            return Join(caseDate, caseText);
        return Default;
    }

    private static string Join(DateOnly? date, string? text) =>
        $"{date?.ToString("dd.MM.yyyy") ?? ""} {text ?? ""}".Trim();
}
