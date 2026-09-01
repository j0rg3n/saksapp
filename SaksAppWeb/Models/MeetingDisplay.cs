namespace SaksAppWeb.Models;

/// <summary>Shared rendering of a meeting's date and optional start time.</summary>
public static class MeetingDisplay
{
    public static string Format(DateOnly date, TimeOnly? time) =>
        time is { } t
            ? $"{date:dd.MM.yyyy} kl. {t:HH\\:mm}"
            : $"{date:dd.MM.yyyy}";

    public static string Format(Meeting meeting) => Format(meeting.MeetingDate, meeting.MeetingTime);
}
