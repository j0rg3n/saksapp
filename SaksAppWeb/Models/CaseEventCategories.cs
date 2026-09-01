namespace SaksAppWeb.Models;

/// <summary>
/// Single source of truth for CaseEvent categories.
///
/// Readers and writers must both draw from here. They drifted apart once already: commit 888c095
/// (Phase 4.5) switched event creation to general/avvik/tiltak while the agenda PDF kept filtering
/// on the legacy "comment" value, which silently dropped every event created after 2026-05-02 from
/// the innkalling while leaving it visible everywhere in the UI.
/// </summary>
public static class CaseEventCategories
{
    public const string General = "general";
    public const string Avvik = "avvik";
    public const string Tiltak = "tiltak";

    /// <summary>Agenda-item anchor, set automatically when an event is created via MeetingEventLink.</summary>
    public const string Meeting = "meeting";

    /// <summary>Ingested from the WhatsApp bot. Not yet surfaced in the agenda PDF.</summary>
    public const string WhatsApp = "whatsapp";

    /// <summary>Legacy only, migrated from the old CaseComment table. Displayed as "Generelt". Never written.</summary>
    public const string LegacyComment = "comment";

    /// <summary>Categories a user may create or edit an event as.</summary>
    public static readonly string[] Writable = [General, Avvik, Tiltak];

    /// <summary>
    /// Case-level categories: everything that belongs to a case's own history, including the
    /// legacy value. Anything reading a case timeline or the agenda PDF must use this set.
    /// </summary>
    public static readonly string[] CaseLevel = [General, Avvik, Tiltak, LegacyComment];

    public static bool IsWritable(string? category) =>
        category is not null && Array.IndexOf(Writable, category) >= 0;

    public static bool IsCaseLevel(string? category) =>
        category is not null && Array.IndexOf(CaseLevel, category) >= 0;

    /// <summary>Legacy "comment" is presented as "general"; everything else passes through.</summary>
    public static string ForDisplay(string category) =>
        category == LegacyComment ? General : category;
}
