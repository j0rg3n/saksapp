using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public sealed class HtmlCaseImporter
{
    private static readonly Regex BracketRegex = new(@"^\s*\[(.*)\]\s*$", RegexOptions.Singleline);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public HtmlCaseImporter(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _config = config;
    }

    public async Task<ImportResult> ImportAsync(string html, CancellationToken ct = default)
    {
        var seedPassword = _config["Import:SeedUserPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
            return ImportResult.Fail("Missing config Import:SeedUserPassword (needed to seed user accounts).");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class,'waffle')]");
        if (table is null)
            return ImportResult.Fail("Could not find the cases table (<table class=\"waffle\">).");

        var rows = table.SelectNodes(".//tr");
        if (rows is null || rows.Count == 0)
            return ImportResult.Fail("No rows found in table.");

        var headerRow = rows.FirstOrDefault(r =>
            GetCells(r).Any(c => CellText(c).Equals("Nr", StringComparison.OrdinalIgnoreCase)) &&
            GetCells(r).Any(c => CellText(c).Equals("Tittel", StringComparison.OrdinalIgnoreCase)));

        if (headerRow is null)
            return ImportResult.Fail("Could not find header row containing 'Nr' and 'Tittel'.");

        var headers = GetCells(headerRow).Select(CellText).ToList();
        var col = BuildColumnMap(headers);

        var dataRows = rows.SkipWhile(r => r != headerRow).Skip(1);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        // Cache meetings created/loaded by date
        var meetingByDate = new Dictionary<DateOnly, Meeting>();

        foreach (var r in dataRows)
        {
            var cells = GetCells(r).ToList();
            if (cells.Count == 0) continue;

            if (!TryGetInt(cells, col, "Nr", out var caseNumber))
            {
                skipped++;
                continue;
            }

            var title = GetString(cells, col, "Tittel")?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                warnings.Add($"Case #{caseNumber}: missing title; skipped.");
                skipped++;
                continue;
            }

            var theme = GetString(cells, col, "Tema")?.Trim();

            var priorityInt = TryGetInt(cells, col, "Prioritet", out var p) ? p : 2;
            var priority = priorityInt switch
            {
                1 => CasePriority.P1,
                2 => CasePriority.P2,
                _ => CasePriority.P3
            };

            var assigneeName = GetString(cells, col, "Ansvarlige")?.Trim();
            var assigneeUserId = await EnsureAssigneeUserAsync(assigneeName, seedPassword, warnings, ct);

            var background = GetString(cells, col, "Bakgrunn")?.Trim();
            var startDate = TryGetDateOnly(cells, col, "Start", out var sd) ? sd : DateOnly.FromDateTime(DateTime.UtcNow);

            var closedDate = TryGetDateOnly(cells, col, "Avsluttet", out var cd) ? cd : (DateOnly?)null;
            var status = closedDate is not null ? CaseStatus.Closed : CaseStatus.Open;

            var tidsfristDate = TryGetDateOnly(cells, col, "Tidsfrist", out var td) ? td : (DateOnly?)null;
            var tidsfristText = tidsfristDate is null ? GetString(cells, col, "Tidsfrist")?.Trim() : null;

            var statusDate = TryGetDateOnly(cells, col, "Siste status dato", out var ssd) ? ssd : (DateOnly?)null;
            var latestStatusTextRaw = GetString(cells, col, "Siste status (fra referat, ellers i [])")?.Trim();
            var nextStepRaw = GetString(cells, col, "Neste skritt (fra referat, ellers i [])")?.Trim();

            var existing = await _db.BoardCases
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.CaseNumber == caseNumber, ct);

            BoardCase entity;
            if (existing is null)
            {
                entity = new BoardCase
                {
                    CaseNumber = caseNumber,
                    Title = title!,
                    Theme = theme,
                    Priority = priority,
                    AssigneeUserId = assigneeUserId,
                    Description = background,
                    StartDate = startDate,
                    Status = status,
                    ClosedDate = closedDate,
                    CustomTidsfristDate = tidsfristDate,
                    CustomTidsfristText = tidsfristText
                };

                _db.BoardCases.Add(entity);
                await _db.SaveChangesAsync(ct);

                await _audit.LogAsync(
                    AuditAction.Create,
                    nameof(BoardCase),
                    entity.Id.ToString(),
                    before: null,
                    after: new { entity.Id, entity.CaseNumber, entity.Title, entity.Priority, entity.AssigneeUserId, entity.Status },
                    reason: "Bootstrap import: create case",
                    ct: ct);

                created++;
            }
            else
            {
                entity = existing;

                var before = new
                {
                    entity.Title, entity.Theme, entity.Priority, entity.AssigneeUserId,
                    entity.Description, entity.StartDate, entity.Status, entity.ClosedDate,
                    entity.CustomTidsfristDate, entity.CustomTidsfristText
                };

                entity.Title = title!;
                entity.Theme = theme;
                entity.Priority = priority;
                entity.AssigneeUserId = assigneeUserId;
                entity.Description = background;
                entity.StartDate = startDate;
                entity.Status = status;
                entity.ClosedDate = closedDate;
                entity.CustomTidsfristDate = tidsfristDate;
                entity.CustomTidsfristText = tidsfristText;

                await _db.SaveChangesAsync(ct);

                var after = new
                {
                    entity.Title, entity.Theme, entity.Priority, entity.AssigneeUserId,
                    entity.Description, entity.StartDate, entity.Status, entity.ClosedDate,
                    entity.CustomTidsfristDate, entity.CustomTidsfristText
                };

                await _audit.LogAsync(
                    AuditAction.Update,
                    nameof(BoardCase),
                    entity.Id.ToString(),
                    before,
                    after,
                    reason: "Bootstrap import: update case",
                    ct: ct);

                updated++;
            }

            // Import bracket notes as comments
            await ImportBracketAsCommentIfNeeded(entity.Id, statusDate, latestStatusTextRaw, warnings, ct);
            await ImportBracketAsCommentIfNeeded(entity.Id, statusDate, nextStepRaw, warnings, ct);

            // If latestStatus is NOT bracketed, treat it as official minutes -> generate meeting/minutes
            if (statusDate is not null && IsOfficialText(latestStatusTextRaw))
            {
                var meeting = await GetOrCreateMeetingAsync(statusDate.Value, meetingByDate, ct);

                await EnsureCaseScheduledAndMinutesEntryAsync(
                    meeting,
                    entity,
                    latestStatusTextRaw!,
                    nextStepRaw,
                    ct);
            }
        }

        // After creating meetings by date, ensure YearSequenceNumber is correct per year (in date order)
        await RenumberMeetingsPerYearAsync(ct);

        return ImportResult.Ok(created, updated, skipped, warnings);
    }

    private async Task<string> EnsureAssigneeUserAsync(string? assigneeName, string seedPassword, List<string> warnings, CancellationToken ct)
    {
        var name = (assigneeName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ""; // you can choose to create an "Unassigned" user instead

        var existing = await _userManager.FindByNameAsync(name);
        if (existing is not null)
            return existing.Id;

        var emailLocal = Regex.Replace(name.ToLowerInvariant(), @"\s+", ".");
        emailLocal = Regex.Replace(emailLocal, @"[^a-z0-9\.\-]+", "");
        if (string.IsNullOrWhiteSpace(emailLocal))
            emailLocal = "user";

        var user = new ApplicationUser
        {
            UserName = name,
            Email = $"{emailLocal}@local.invalid",
            EmailConfirmed = true,
            FullName = name
        };

        var result = await _userManager.CreateAsync(user, seedPassword);
        if (!result.Succeeded)
        {
            warnings.Add($"Could not create user '{name}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            return "";
        }

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(IdentityUser),
            user.Id,
            before: null,
            after: new { user.Id, user.UserName, user.Email },
            reason: "Bootstrap import: seed assignee user",
            ct: ct);

        return user.Id;
    }

    private async Task ImportBracketAsCommentIfNeeded(int boardCaseId, DateOnly? fallbackDate, string? raw, List<string> warnings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var m = BracketRegex.Match(raw);
        if (!m.Success)
            return;

        var text = m.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var createdAt = fallbackDate is not null
            ? new DateTimeOffset(fallbackDate.Value.Year, fallbackDate.Value.Month, fallbackDate.Value.Day, 12, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

        var comment = new CaseComment
        {
            BoardCaseId = boardCaseId,
            CreatedAt = createdAt,
            CreatedByUserId = _audit.GetActorUserId() ?? "", // importer actor
            Text = text
        };

        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(CaseComment),
            comment.Id.ToString(),
            before: null,
            after: new { comment.Id, comment.BoardCaseId, comment.CreatedAt, comment.CreatedByUserId, comment.Text },
            reason: "Bootstrap import: bracket note -> comment",
            ct: ct);
    }

    private static bool IsOfficialText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return !BracketRegex.IsMatch(raw);
    }

    private async Task<Meeting> GetOrCreateMeetingAsync(DateOnly meetingDate, Dictionary<DateOnly, Meeting> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(meetingDate, out var m))
            return m;

        var existing = await _db.Meetings.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.MeetingDate == meetingDate, ct);
        if (existing is not null)
        {
            cache[meetingDate] = existing;
            return existing;
        }

        var meeting = new Meeting
        {
            MeetingDate = meetingDate,
            Year = meetingDate.Year,
            YearSequenceNumber = 1 // temporary; will be renumbered
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            nameof(Meeting),
            meeting.Id.ToString(),
            before: null,
            after: new { meeting.Id, meeting.MeetingDate, meeting.Year, meeting.YearSequenceNumber },
            reason: "Bootstrap import: created meeting from official status",
            ct: ct);

        cache[meetingDate] = meeting;
        return meeting;
    }

    private async Task EnsureCaseScheduledAndMinutesEntryAsync(Meeting meeting, BoardCase boardCase, string officialStatusText, string? nextStepRaw, CancellationToken ct)
    {
        // schedule
        var mc = await _db.MeetingCases
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.MeetingId == meeting.Id && x.BoardCaseId == boardCase.Id, ct);

        if (mc is null)
        {
            var maxOrder = await _db.MeetingCases
                .Where(x => x.MeetingId == meeting.Id)
                .MaxAsync(x => (int?)x.AgendaOrder, ct);

            mc = new MeetingCase
            {
                MeetingId = meeting.Id,
                BoardCaseId = boardCase.Id,
                AgendaOrder = (maxOrder ?? 0) + 1,
                AgendaTextSnapshot = boardCase.Description ?? boardCase.Title
            };

            _db.MeetingCases.Add(mc);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(
                AuditAction.Create,
                nameof(MeetingCase),
                mc.Id.ToString(),
                before: null,
                after: new { mc.Id, mc.MeetingId, mc.BoardCaseId, mc.AgendaOrder },
                reason: "Bootstrap import: scheduled case into meeting",
                ct: ct);
        }

        // minutes header row
        var minutes = await _db.MeetingMinutes.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.MeetingId == meeting.Id, ct);
        if (minutes is null)
        {
            minutes = new MeetingMinutes { MeetingId = meeting.Id };
            _db.MeetingMinutes.Add(minutes);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(
                AuditAction.Create,
                nameof(MeetingMinutes),
                minutes.Id.ToString(),
                before: null,
                after: new { minutes.Id, minutes.MeetingId },
                reason: "Bootstrap import: created minutes record",
                ct: ct);
        }

        // minutes entry per case for that meeting
        var entry = await _db.MeetingMinutesCaseEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.MeetingId == meeting.Id && x.BoardCaseId == boardCase.Id, ct);

        if (entry is null)
        {
            entry = new MeetingMinutesCaseEntry
            {
                MeetingId = meeting.Id,
                MeetingCaseId = mc.Id,
                BoardCaseId = boardCase.Id,
                OfficialNotes = officialStatusText,
                FollowUpText = IsOfficialText(nextStepRaw) ? nextStepRaw : null,
                Outcome = MeetingCaseOutcome.Continue
            };

            _db.MeetingMinutesCaseEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(
                AuditAction.Create,
                nameof(MeetingMinutesCaseEntry),
                entry.Id.ToString(),
                before: null,
                after: new { entry.Id, entry.MeetingId, entry.BoardCaseId },
                reason: "Bootstrap import: created minutes entry",
                ct: ct);
        }
    }

    private async Task RenumberMeetingsPerYearAsync(CancellationToken ct)
    {
        var meetings = await _db.Meetings.IgnoreQueryFilters()
            .OrderBy(x => x.MeetingDate)
            .ToListAsync(ct);

        var grouped = meetings.GroupBy(m => m.Year);
        foreach (var g in grouped)
        {
            var i = 1;
            foreach (var m in g.OrderBy(x => x.MeetingDate))
            {
                if (m.YearSequenceNumber != i)
                {
                    var before = new { m.YearSequenceNumber };
                    m.YearSequenceNumber = i;
                    await _audit.LogAsync(
                        AuditAction.Update,
                        nameof(Meeting),
                        m.Id.ToString(),
                        before,
                        after: new { m.YearSequenceNumber },
                        reason: "Bootstrap import: renumber meetings per year",
                        ct: ct);
                }
                i++;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // --- helpers (same as before) ---
    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim();
            if (!map.ContainsKey(h) && !string.IsNullOrWhiteSpace(h))
                map[h] = i;
        }
        return map;
    }

    private static IEnumerable<HtmlNode> GetCells(HtmlNode row) =>
        row.SelectNodes("./td") ?? Enumerable.Empty<HtmlNode>();

    private static string CellText(HtmlNode cell) =>
        HtmlEntity.DeEntitize(cell.InnerText).Trim();

    private static string? GetString(IReadOnlyList<HtmlNode> cells, IReadOnlyDictionary<string, int> col, string header)
    {
        if (!col.TryGetValue(header, out var idx)) return null;
        if (idx < 0 || idx >= cells.Count) return null;
        var t = CellText(cells[idx]);
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static bool TryGetInt(IReadOnlyList<HtmlNode> cells, IReadOnlyDictionary<string, int> col, string header, out int value)
    {
        value = default;
        var s = GetString(cells, col, header);
        return s is not null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDateOnly(IReadOnlyList<HtmlNode> cells, IReadOnlyDictionary<string, int> col, string header, out DateOnly value)
    {
        value = default;
        var s = GetString(cells, col, header);
        if (string.IsNullOrWhiteSpace(s)) return false;
        return DateOnly.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}

public record ImportResult(bool Success, int Created, int Updated, int Skipped, IReadOnlyList<string> Warnings, string? Error)
{
    public static ImportResult Ok(int created, int updated, int skipped, IReadOnlyList<string> warnings)
        => new(true, created, updated, skipped, warnings, null);

    public static ImportResult Fail(string error)
        => new(false, 0, 0, 0, Array.Empty<string>(), error);
}
