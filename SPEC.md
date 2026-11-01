# SaksApp - Specification

## Overview

SaksApp is a case management system for board meetings. It enables users to manage cases, schedule them on meeting agendas, generate meeting documents (PDFs), and track the history of decisions and follow-ups.

## User interface

Language is Norwegian Bokmål.

Layouts must work both on phone and desktop.

## Core Concepts

### Case (Sak)
A case represents an item to be discussed or decided upon. A case has:
- **CaseNumber** - Unique identifier (auto-incrementing)
- **Title** - Brief title of the case
- **Description** - Full description/details of the case
- **Theme** - Optional theme/category
- **Priority** - Priority level (1=lowest, 2, 3=highest)
- **Status** - Open or Closed
- **Assignee** - User responsible for the case
- **Tidsfrist** - Deadline (date and/or text)

### Meeting (Møte)
A meeting where cases are discussed. A meeting has:
- **MeetingDate** - Date of the meeting
- **MeetingTime** - Time of day the meeting starts (optional; meetings are normally planned with both a date and a time)
- **Year** - Year (derived from MeetingDate)
- **YearSequenceNumber** - Sequence number within the year (e.g., 1st meeting, 2nd meeting)
- **Location** - Physical or virtual location

### Agenda Item (Sak på møtet)
A case scheduled for a specific meeting. An agenda item has:
- **AgendaOrder** - Order within the meeting
- **AgendaTextSnapshot** - Text snapshot of the case's agenda text at time of scheduling
- **TidsfristOverride** - Optional override of the case's default tidsfrist

## Layouts

Visual layouts and wireframes are documented in [LAYOUTS.md](./LAYOUTS.md).

## User Functions

### Home Page
- The home page (`/`) displays a rendered Markdown document explaining how the app works.
- The content lives in `SaksAppWeb/Content/home.md` — editable without redeploying.
- Rendering: the controller reads `home.md` at request time, converts it to HTML via **Markdig**, and passes it to the view as an `HtmlString`.
- The page is accessible to unauthenticated users (no `[Authorize]` required).
- The view renders the HTML inside the standard layout with no additional chrome.

### Authentication
- Users log in via ASP.NET Identity
- Password reset via email
- Role-based access (all authenticated users have similar access)

### Case Management

**List Cases**
- View all open cases (default view)
- Option to include closed cases
- Displayed columns: CaseNumber, Title, Theme, Priority, Status, Assignee, Tidsfrist
- Click to open case details

**Create Case**
- Form to create new case with: Title, Description, Theme, Priority, Assignee
- Auto-assigns next available CaseNumber

**Edit Case**
- Modify any case field
- Track changes in audit log

**View Case Details**
- Full case information
- Chronological timeline of all events linked to the case:
  - Case events (general/avvik/tiltak/legacy comment) with category badge and author
  - Meeting minutes entries with outcome, referat, vedtak, oppfølging
  - Each event shows badges for other cases the event is linked to (with links)
- Attachment list per event; upload/remove attachments on editable events

**Add Case Event**
- Form with category selector: Generelt (default), Avvik, Tiltak
- Free text content
- Created event is linked to the current case via CaseEventCase

**Edit/Delete Case Event**
- Available for general/avvik/tiltak events (and legacy comment)
- Accessible from the case timeline

**Add Attachment**
- Upload files (max 10MB) to any editable case event
- Files stored in database
- **Only PDF and image content types are accepted.** Enforced at upload time on every path that
  creates an attachment (`CasesController.IsAllowedContentType`, reused by the meeting and
  Hendelseslogg upload actions); signed minutes must specifically be a PDF.
- This is an invariant the PDF generators depend on: the agenda and minutes embed attachments by
  appending PDF pages or drawing images, so any other type would be listed in the attachment index
  without appearing in the document. Relaxing the accepted types requires handling them there
  first.
- **An unreadable attachment must not break the agenda or minutes PDF.** A PDF or image that
  passes the type check can still fail to parse (corrupt file, password-protected PDF, format
  the PDF library does not support). In that case the attachment keeps its cover page — so the
  attachment index and its page numbers stay correct — and the cover page carries a fixed
  Norwegian note that the file could not be inserted and that the original is in the case. None
  of the attachment's pages are included (no half-copied attachments). The exception text is
  **not** printed in the document; it is logged as a warning with the file name and attachment
  number. Only failures while parsing the attachment are handled this way; other errors still
  fail the export.

### Meeting Management

**List Meetings**
- View all meetings, sorted by date (newest first)
- Displayed columns: Date (with time when set), Year/Sequence, Location

**Create Meeting**
- Form with: MeetingDate, MeetingTime, YearSequenceNumber, Location

**Edit Meeting**
- Modify meeting date, meeting time, sequence number, and location

**Meeting Details / Agenda**
- View meeting with its agenda (scheduled cases)
- Add cases to agenda from list of open cases
- Reorder agenda items (move up/down)
- Edit agenda item (agenda text snapshot, tidsfrist override)
- Remove case from agenda
- Download agenda PDF
- Download assignee reminder PDF

**Meeting Minutes**
- Record meeting outcomes per agenda item
- Fields: OfficialNotes, DecisionText, FollowUpText
- Upload attachments to minutes
- Mark outcome: Fortsetter, Avsluttet, Utsatt, Orientering, Diskusjon
  - **Diskusjon** — item was discussed with no decision intended; no follow-up action expected
- Download minutes PDF

**Utvikling siden innkallingen**
- In the minutes view, each agenda item shows a read-only section «Utvikling siden innkallingen» listing CaseEvents on the case that occurred after the last agenda PDF was generated (`Meeting.AgendaPdfGeneratedAt`) and up to and including the meeting date.
- If `AgendaPdfGeneratedAt` is null (agenda PDF was never generated), this section is omitted.
- Events that occur after the meeting date belong to the next meeting and are not shown.
- The section is informational only — no editing, no outcome fields.
- If any event has attachments, each attachment is shown as a link opening in a new tab (`target="_blank"`), so the minutes writer can reference the document without losing their place.

### Eventuelt (Extra Items)
- Cases can be marked as "Eventuelt" (extra items)
- Eventuelt cases appear in meeting minutes but not in the agenda PDF
- They are full cases, just with special status

### CaseEvent (Unified Event System)

CaseEvent is the unified model replacing CaseComment, MeetingMinutesCaseEntry, and the proposed BoardEvent.

**Structure:**
- **CaseEvent** - The core event entity
  - `CreatedAt` - Timestamp
  - `Content` - Text content
  - `Category` - Event category: `general`, `avvik`, `tiltak`, `meeting`, `whatsapp`
    - `general` — standard note or observation, the default for case-level events
    - `avvik` — deviation (HMS)
    - `tiltak` — measure/corrective action
    - `meeting` — entry linked to a meeting agenda item (set automatically)
    - `whatsapp` — ingested from WhatsApp bot (future)
    - `comment` — **legacy only**, migrated from old CaseComment table; displayed as "Generelt"
  - `Attachments` - Associated files

- **CaseEventCase** - Many-to-many: a CaseEvent can be linked to multiple cases
  - An event created from a case is linked to that case
  - An event from Hendelseslogg linked to one or more case numbers is linked to those cases
  - The two are equivalent — linking an event to a case from Hendelseslogg is the same as creating it from the case

- **MeetingEventLink** - Links CaseEvents to meetings with meeting-specific metadata
  - `MeetingId` - The meeting this event is associated with
  - `CaseEventId` - The linked event
  - `AgendaOrder` - Index of the case in the meeting agenda
  - `OfficialNotes` - Referat (meeting minutes notes)
  - `DecisionText` - Vedtak (decision)
  - `FollowUpText` - Oppfølging (follow-up)
  - `IsEventuelt` - Whether this is an Eventuelt item (no CaseEventCase link)

**Category rules:**
- Events created from a case view default to `general`; user can choose `avvik`, `tiltak`, or `general`
- Events created from Hendelseslogg can be `avvik`, `tiltak`, or `general`
- `meeting` is set automatically when a CaseEvent is created via MeetingEventLink
- `comment` is a legacy category treated as `general` in display; no new events are created with this category

**Multi-case linking:**
- A CaseEvent can be linked to multiple cases simultaneously
- When viewing a case timeline, events linked to other cases show badges for those other cases (with links)
- Linking from Hendelseslogg (by case number) and from the case view are functionally equivalent

**Attachments:**
- All event categories support file attachments
- Attachments can be uploaded and removed from the case timeline view for any event the user can edit
- Editable categories: `general`, `avvik`, `tiltak` (and legacy `comment`)

**Notes:**
- A CaseEvent has a single timestamp and cannot appear in multiple meetings
- Meeting-specific outcomes (referat, vedtak, oppfølging) are stored on MeetingEventLink, not on CaseEvent
- MeetingMinutes remains separate for meeting header information (date, approval status)

**Migration:**
- CaseComment → CaseEvent (Category = "comment", legacy)
- MeetingMinutesCaseEntry → CaseEvent (Category = "meeting") linked via MeetingEventLink

### WhatsApp Integration

A bot is added to one or more WhatsApp groups. Incoming messages are ingested and stored as CaseEvents.

**Message ingestion rules:**

- Each text message creates a new CaseEvent with `Category = "whatsapp"`.
- Attached images and files (media messages) are stored as Attachments on the most recent CaseEvent from the same sender in the same group, provided it was created within the configured grouping window.
- If no such event exists yet in the window, a new CaseEvent is created (with empty content) to host the attachment.
- **Grouping window** (default: 5 minutes, configurable per group): consecutive messages and media from the same sender within the window are merged into the same CaseEvent. Each subsequent message's text is appended (newline-separated) and each media item is added as an attachment.
- The CaseEvent timestamp is the time of the **first** message in the group.

**Case linking:**

- Any message containing `#<number>` (e.g. `#46`) causes the resulting CaseEvent to be linked to that case via `CaseEventCase`. Multiple hashtags in one message link to multiple cases.
- The raw hashtag text is preserved in the CaseEvent content.

**Configuration:**

- Grouping window duration (minutes) — per group or global default (2 min).
- Which groups the bot is active in is controlled by the group's WhatsApp ID being present in the bot configuration.

**Data model:**

- CaseEvent: `Source = "whatsapp"`, `SourceGroupId = <WhatsApp group JID>`, `SourceSenderId = <sender JID>`
- Existing Attachment model is reused; `UploadedByUserId` is set to a system user or left null for bot-ingested files.

**Technical approach:**

- WhatsApp connectivity via **whatsmeow** (Go library) or **Baileys** (Node.js), running as a sidecar process.
- Sidecar calls an internal HTTP endpoint on SaksAppWeb (`POST /api/whatsapp/ingest`) with a JSON payload.
- The ingest endpoint is authenticated with a shared secret (configurable).
- Ingest endpoint logic:
  1. Look up or create the grouping buffer (keyed by group + sender + open window).
  2. Append text / attach media.
  3. Flush buffer to CaseEvent + Attachments when window expires (background timer) or when a new message arrives outside the window.

**UI:**

- CaseEvents with `Source = "whatsapp"` are displayed in the case timeline with a WhatsApp icon and the sender's display name (mapped from `SourceSenderId` via a configurable name map or left as phone number).
- No separate admin UI for bot management in initial version; configuration is in appsettings.

### HMS Deviation and Measures Log

**Overview**
- Dedicated view for deviations and measures (Helse, Miljø, Sikkerhet)
- Shows all events categorized as deviations and measures
- Can reference related cases
- Typically does not include meetings (decisions not made there)

**Avvik–Tiltak linking**
- An `avvik` event can be linked to the `tiltak` event(s) that address it, and a `tiltak` may
  address more than one `avvik` — many-to-many, via a `CaseEventRelation` join
- The relation is between CaseEvents and is independent of which cases each event is linked to
- Both the HMS view and the case timeline show the linked counterpart(s), with links

**Avvik status**
- An `avvik` is Open or Closed, recorded on the event as `IsClosed`, `ClosedAt`, `ClosedByUserId`
- Closing is explicit. Linking a `tiltak` does **not** close the `avvik` — a measure is often
  registered while the deviation is still open
- The HMS view filters by status and defaults to showing open `avvik`
- Status applies only to `avvik`; `tiltak` events have no status of their own

**Excel export**
- The HMS view offers "Eksporter til Excel", producing an `.xlsx` of the currently filtered set
  (with no filter applied, that is every `avvik` and `tiltak`)
- One row per event, columns: Type (Avvik/Tiltak), Dato, Innhold, Saksnumre, Status, Lukket dato,
  Koblede tiltak/avvik, Registrert av
- Generated server-side; no external service involved

### PDF Generation

**Unified visual style**

Compact. Use font sizes, styles, and spacing to visually distinguish different sections and speed up visual search.

**Layouts**: See [LAYOUTS.md](./LAYOUTS.md) for detailed visual mockups and specifications.

**PDF typography** (agenda and minutes; source: typography review 2026-11-01)
- One size per heading level, never larger than its parent: document title 16 pt Bold, section
  ("Agenda", "Vedlegg") 12 pt Bold, agenda item title 11 pt Bold, every sub-label under an item
  (`Forrige møte`, `Status:`, `Vedtak:`, `Oppfølging:`, `Tidsfrist:`, `Til møtet:`,
  `Historikk siden forrige møte:`) 10 pt Bold. Body 10 pt Regular.
- **Keep with next**: a heading or sub-label is never the last line on a page; it moves to the
  next page together with at least two lines of what follows. A body line ending in `:` (a
  user-written label such as `Plan:`) gets the same treatment.
- **Dates** in generated text use `dd.MM.yyyy` — meeting dates, tidsfrist dates and history
  entry dates alike. History entry dates are shown in the app time zone (`App:TimeZone`), not UTC.
  User-typed text is not rewritten.
- **History entries**: `<date> [badge] <text>` — date and text in italic, badge only for
  avvik/tiltak, no colon after the date in either case. The first line fills the available width
  (measured, not a fixed character count); following lines wrap with a hanging indent.
- **Hanging indent** for user-written list lines: a paragraph starting with `1.`, `1)`, `-`,
  `*` or `•` wraps its continuation lines under the text, not under the marker.
- **Unsupported characters**: the embedded font (Liberation Sans) has no emoji. Emoji and other
  pictographs (characters outside the Basic Multilingual Plane, U+2600–U+27BF, variation
  selectors, zero-width joiner) are removed before drawing rather than rendered as blanks.
- **Attachment numbering**: an attachment is identified by its label (`Vedlegg 2.1`) everywhere
  — in text references, the attachment index and the attachment's caption page. The sequential
  number is internal only.
- **Attachment index**: `Vedlegg <label>: <file name>` on the left, the start page as `s. <n>`
  right-aligned at the right margin. A file name too long for the line is shortened with `…`.
- **Attachment caption**: identical for PDF and image attachments — `Vedlegg <label>` in 12 pt
  Bold, file name below in 14 pt Bold, wrapped to the page width (breaking inside a word only when
  the word alone is wider than the line).
- Not in scope for the generator: spelling and wording in user-entered text, and anything inside
  embedded third-party documents.

**Vedleggsdeduplisering**
- If the same attachment (by ID) is referenced by multiple agenda items, it must appear only once in the agenda PDF and once in the minutes PDF.
- Preferred placement: list it under the first agenda item that references it; subsequent items show a cross-reference note.

**Agenda PDF**
- For each agenda item: Title, CaseNumber, Assignee
- Case description (in italics)
- Agenda text snapshot, under the sub-label `Til møtet:`, only if different from the description.
  Shown **once** — it was previously also printed as an unlabeled paragraph under the description.
- Tidsfrist
- Previous meeting follow-up (if applicable)
- Comments since last meeting («Historikk siden forrige møte»)
  - Includes case-level events in categories `general`, `avvik`, `tiltak`, and legacy `comment`.
    `meeting` events are excluded (they are the agenda-item anchors themselves). `whatsapp`
    events are excluded for now.
  - Window: events created after the most recent meeting at which **that case** appeared on the
    agenda, up to the start of the current meeting. This is per-case, not "since the previous
    meeting" globally. A case never previously on an agenda includes its full history.
  - The window ends at the meeting's `MeetingDate` + `MeetingTime`. When no time is recorded the
    window ends at the end of that day, so a missing time never silently drops an event.
  - `MeetingTime` is wall-clock local time, entered and displayed as such. It is converted to an
    absolute instant using the `App:TimeZone` setting (default `Europe/Oslo`) before comparison
    against `CaseEvent.CreatedAt`, which is UTC. If the configured zone cannot be resolved the app
    falls back to UTC and logs a warning rather than failing to start.
  - The category set must match the case timeline (`ICaseQueryService`) — the agenda PDF must
    never filter on a narrower set.
  - Events in categories `avvik` and `tiltak` are preceded by a coloured badge naming the
    category, using the same palette as the web UI: Avvik `#DC3545`, Tiltak `#FFC107`.
    `general` and legacy `comment` events get **no** badge — they are the ordinary case, and
    badging them would defeat the point of making deviations and measures stand out.
- Attachment references

**Minutes PDF**
- Meeting header (date, location)
- Attendance
- Approval of previous minutes
- For each agenda item: Title, CaseNumber, Decision, Follow-up, Attachments
- Eventuelt section
- Next meeting date

### User Management

**Roles**
- Two roles: **Admin** and **Regular user**
- The first user to register is automatically an admin and auto-approved
- All subsequent registrations start as unapproved regular users
- An unapproved user who logs in sees a "pending approval" page and cannot access the app
- Only admins can approve/unapprove users and toggle admin status
- An admin cannot remove their own admin status

**List Users (Admin only)**
- View all users with full name, email, approval status, admin status
- Approve or unapprove pending users
- Toggle admin status for other users
- Edit user (full name, email)

**Registration flow**
- New user registers with email + password (standard ASP.NET Identity)
- Account is created with `IsApproved = false`
- User sees "Din konto venter på godkjenning" on login until approved
- Any admin can approve the user from the user list

### Google Drive Backup

**Overview**
- A single admin links one Google Drive account; the app uploads compressed SQLite backups there
- Offsite copies are what allow local retention to stay short
- Restore is a documented manual procedure, not a UI feature

**Drive linking**
- Settings page (`/settings`): "Koble til Google Drive" starts an OAuth 2.0 flow (scope `drive.file`)
- Admin-only. Access and refresh tokens stored encrypted via ASP.NET Data Protection
  (`IDataProtector`, purpose `"DriveTokens"`)
- Shows link status, last upload result, and a "Koble fra" button that deletes the tokens

**Upload schedule**
- Background service checks every `GoogleDrive:BackupIntervalDays` (default 1) whether an upload is due
- Uploads the newest existing local backup — it does not create a separate snapshot
- Uploaded as `saksapp-backup-YYYY-MM-DD.db.gz`
- Each attempt recorded in `DriveBackupLog` (`AttemptedAt`, `BackupDate`, `Success`, `ErrorMessage`)
- No-op when no Drive is linked

**Drive-side retention**
- Keeps the newest `GoogleDrive:KeepCount` uploads (default 30), deleting older ones
- The `drive.file` scope only grants access to files this app created, so deletion cannot touch
  the user's other Drive content

**Restore (manual)**
- Download the chosen `saksapp-backup-*.db.gz` from Drive, gunzip, stop the container, replace
  `db/app.db`, start the container. Migrations apply on startup as usual.
- No restore UI, no staging file, no startup swap flag, no session invalidation logic

**Security**
- Tokens encrypted at rest; linking and unlinking are admin-only and written to the audit log

### Audit Log

**View Audit Log**
- Track all changes to: Cases, Meetings, Agenda Items
- Show: Timestamp, User, Action, Entity, Before/After values

### Case Import

**Import from HTML**
- Import cases from HTML export
- Map HTML fields to case fields
- Option to create new cases or update existing (by case number)

## Data Model

### Unified Event System

The unified event model replaces the previous separate entities (CaseComment, MeetingMinutesCaseEntry, BoardEvent, BoardEventCase).

### Entities
- **ApplicationUser** - Extended identity user
  - `IsApproved` - bool, default false; first user auto-true
  - `IsAdmin` - bool, default false; first user auto-true
- **DriveToken** - The single admin Google Drive link
  - `UserId`, `AccessToken` (encrypted), `RefreshToken` (encrypted), `TokenExpiry`, `LinkedAt`
- **DriveBackupLog** - Record of each backup upload attempt
  - `AttemptedAt`, `BackupDate`, `Success`, `ErrorMessage`
- **BoardCase** - Case information
- **CaseEvent** - Unified event entry (replaces CaseComment and BoardEvent)
  - Linked to Case via CaseEventCase
  - Optional MeetingEventLink for meeting context
- **CaseEventCase** - Many-to-many link between CaseEvent and BoardCase
- **MeetingEventLink** - Links CaseEvent to Meeting with meeting-specific metadata (replaces MeetingCase and MeetingMinutesCaseEntry)
  - Includes AgendaOrder, OfficialNotes, DecisionText, FollowUpText
- **Attachment** - File attachments (linked to CaseEvent or standalone)
- **Meeting** - Meeting metadata
- **MeetingMinutes** - Minutes header info (approval status, etc.)
- **PdfGeneration** - Log of generated PDFs

### Soft Deletes
All major entities support soft delete (IsDeleted, DeletedAt, DeletedByUserId).

## Technical Notes

### Database
- SQLite database
- Entity Framework Core for data access
- Auto-applied migrations on startup

### Email
- SMTP configuration for sending emails
- Used for password reset functionality

### Deployment hygiene

- `deployment/deploy-upcloud.sh` removes the transferred image tarball after `docker load` and
  prunes dangling images. Without this each deploy left ~1 GB of untagged image plus a 121 MB
  tarball behind; that accumulation, not the backups, was the largest consumer when the disk
  filled on 2026-09-08.
- Disk headroom is a standing operational concern: SQLite reports a full disk as
  `SQLITE_READONLY` ("attempt to write a readonly database"), which reads like a permissions
  fault and sends diagnosis in the wrong direction.

### Transport security

- The application is served over **HTTPS only**, in front of a **Caddy** reverse proxy that obtains
  and renews Let's Encrypt certificates automatically.
- The hostname comes from `SAKSAPP_DOMAIN` (set in `auth/secret.env`); an A record for it must
  point at the VM. A bare IP address cannot be used: Let's Encrypt will not certify one and Google
  rejects it as an OAuth redirect host.
- Caddy publishes ports 80 and 443. Port 80 exists only to satisfy the ACME HTTP challenge and to
  redirect to HTTPS; Caddy does both by default.
- The application container does **not** publish a public port. It binds `127.0.0.1:8080` on the
  host for local debugging only, so the only public path in is through the proxy.
- The app trusts `X-Forwarded-For` and `X-Forwarded-Proto` via `UseForwardedHeaders`. Trusting
  them unconditionally is safe *because* the app port is not publicly reachable — if that ever
  changes, the proxy must be pinned with `KnownProxies` instead.
- Authentication cookies are marked `Secure` outside Development, and HSTS is enabled. HSTS was
  previously a no-op: browsers ignore the header when it arrives over plain HTTP.

### Data Protection
- ASP.NET Data Protection for antiforgery tokens
- Keys persisted to filesystem

### Build context
- The Docker build context must exclude `auth/`, `db/`, `dp_keys/` and `*.tar`. These are already
  excluded from git; `.dockerignore` must mirror `.gitignore` so credentials, Data Protection keys
  and the live database never enter a build layer.
- Only the final multi-stage image is deployed, but intermediate layers are readable by anyone
  with access to the Docker daemon.

### Background Services
- Database backup service — periodic SQLite Online Backup API snapshots to `db/Backups/`
  - Interval: `Backup:IntervalMinutes` (default 60)
  - Each snapshot is gzip-compressed and named `app-<yyyyMMdd-HHmmss>.sqlite.gz`
  - Retention is bounded by BOTH limits, whichever binds first:
    - `Backup:MaxFiles` (default 48) — oldest deleted beyond this count
    - `Backup:MaxTotalBytes` (default 2 GB) — oldest deleted until the folder fits
  - The newest backup is never deleted, even if it alone exceeds `MaxTotalBytes`
  - A snapshot is compressed to a temporary name and moved into place atomically, so an
    interrupted run can never leave a truncated file that looks like a valid backup
  - Retention runs both before and after taking a snapshot: pruning first is what lets the service
    reclaim space on a full disk, which is precisely when the snapshot write would otherwise fail
  - Retention considers `.sqlite`, `.sqlite.gz` and `.sqlite.bz2` so historical formats are swept
    rather than orphaned
  - Ordering for deletion is by the timestamp in the filename, not filesystem timestamps
  - **Free-space floor**: if writing a snapshot would leave less than `Backup:MinFreeBytes`
    (default 1 GB) available on the backup volume, the backup is skipped and a warning logged.
    The backup system must never be the thing that fills the disk it is protecting.
  - Sizing note: the budget is a share of a small volume. The production VM has a 9.8 GB disk and
    the database grows roughly 8 MB/week from attachments, so `MaxTotalBytes` must be set per
    deployment rather than relying on the default
  - `deployment/download-backups.sh` mirrors these rules for the downloaded archive: it
    deduplicates and gzips new snapshots and prunes by the same two limits
- WhatsApp ingest buffer flush service - background timer that flushes pending message groups when the grouping window expires

### Testing

**Coverage Target**
- Minimum 75% code coverage for business logic
- Focus on services, controllers, and data transformation logic

## Testability Architecture

To achieve the 75% coverage target, the codebase follows these architectural patterns:

### Service Layer Abstraction
All business logic is accessed through interfaces, enabling mocking in tests:

```csharp
// Query Services - Extract complex queries from controllers
public interface ICaseQueryService
{
    Task<IReadOnlyList<BoardCase>> GetFilteredCasesAsync(CaseStatus? status, string? assigneeUserId, bool showClosed, CancellationToken ct);
    Task<CaseDetailsVm> GetCaseDetailsAsync(int id, CancellationToken ct);
}

public interface IMeetingQueryService
{
    Task<IReadOnlyList<Meeting>> GetAllMeetingsAsync(CancellationToken ct);
    Task<Meeting?> GetMeetingWithAgendaAsync(int id, CancellationToken ct);
    Task<MeetingMinutesVm?> GetMeetingWithMinutesAsync(int id, CancellationToken ct);
}

public interface IUserDisplayService
{
    Task<Dictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct);
}

// Generator Services - Abstract external dependencies
public interface IPdfGenerator
{
    Task<byte[]> GenerateAgendaPdfAsync(int meetingId, CancellationToken ct);
    Task<byte[]> GenerateMinutesPdfAsync(int meetingId, CancellationToken ct);
}

public interface IHtmlCaseParser
{
    ParsedCaseData[] Parse(string html);
}

public interface IBackupService
{
    Task CreateBackupAsync(CancellationToken ct);
}
```

### Dependency Injection
All services are registered in Program.cs and injected via constructor:
- Scoped: ICaseQueryService, IMeetingQueryService, IUserDisplayService
- Singleton: IPdfGenerator, IBackupService

### Mocking Strategy
- **Database**: Use SQLite in-memory provider for integration tests
- **Email**: Mock IEmailSender interface
- **PDF**: Mock IPdfGenerator or use test implementation
- **User Manager**: Use TestUserManager implementation
- **Configuration**: Inject test values via IConfiguration

### Test Organization
- Unit tests for isolated logic (services, parsers)
- Integration tests for database operations
- Controller tests for HTTP request/response handling

---

## Implementation Phases

### Phase 0: Test Infrastructure (COMPLETE)
- Test project with xUnit, Moq, EF Core SQLite
- Initial tests for controllers and services
- Current coverage: ~9% (target: 75%)

### Phase 0.1: Testability Refactoring (NEXT)
Extract query logic and abstract external dependencies to enable effective mocking.

See TODO.md for detailed tasks.

---

## Pentest Design: Phase 6A (User Roles and Approval)

This section documents the penetration test plan for the Phase 6A authentication and authorization feature. Each test case includes: the attack vector, how to test it, and the expected (correct) outcome.

### Attack Surface

| Component | Entry points |
|-----------|-------------|
| `RequireApprovedUserMiddleware` | All authenticated routes except `/PendingApproval/*`, `/Account/*`, `/Identity/*`, static assets |
| `AdminOnlyFilter` | `UsersController` (all actions) |
| `AppUserManager.CreateAsync` | Registration form (`/Identity/Account/Register`) |
| `UsersController` (Approve/Unapprove/ToggleAdmin) | POST forms in `/Users/Index` |
| `PendingApprovalController` | `/PendingApproval/Index` |

### Test Cases

#### T6A-01 — Approval bypass via direct URL
**Threat**: Unapproved authenticated user navigates directly to a protected page.  
**How to test**: Register a new account (do not have it approved), log in, then GET `/Cases/Index` directly.  
**Expected**: 302 redirect to `/PendingApproval/Index`. No case data returned.  
**Failure indicator**: Any 200 response with app content.

#### T6A-02 — Approval bypass via middleware-exempt paths
**Threat**: Unapproved user crafts a URL that looks like an exempt path but accesses app functionality.  
**How to test**: As unapproved user, try GETs to `/Account/foo/../../Cases`, `/css/../Cases/Index`, `/Identity/../Cases`.  
**Expected**: These paths should not resolve to protected actions (ASP.NET routing + Kestrel normalise paths before routing). Verify each returns 302 to pending-approval or 404, never app content.  
**Failure indicator**: App content served to unapproved user through a path-traversal-style URL.

#### T6A-03 — Admin action without admin status
**Threat**: Regular (approved but non-admin) user calls `POST /Users/Approve` or `POST /Users/ToggleAdmin`.  
**How to test**: Log in as a non-admin approved user; POST to `/Users/Approve?id=<some-user-id>` with a valid CSRF token.  
**Expected**: 403 Forbidden from `AdminOnlyFilter`.  
**Failure indicator**: 200 or 302 indicating the action executed.

#### T6A-04 — Admin action as unapproved user
**Threat**: Unapproved user calls admin endpoints directly, bypassing the approval middleware through middleware ordering.  
**How to test**: As unapproved authenticated user, POST to `/Users/Approve?id=<some-user-id>`.  
**Expected**: 302 to `/PendingApproval/Index` (middleware fires before `AdminOnlyFilter`).  
**Failure indicator**: 403 would be acceptable too; 200/redirect to Index would indicate bypass.

#### T6A-05 — Self-approval via mass assignment
**Threat**: A user sets `IsApproved=true` or `IsAdmin=true` by injecting form fields into a POST request.  
**How to test**: POST to `/Users/Edit?id=<own-id>` with extra fields `IsApproved=true&IsAdmin=true`.  
**Expected**: `Edit` POST only processes `fullName`; `IsApproved`/`IsAdmin` are not model-bound there (they are set only via explicit `Approve`/`ToggleAdmin` actions). Verify the DB values are unchanged.  
**Failure indicator**: User's `IsApproved` or `IsAdmin` flips to true after the request.

#### T6A-06 — CSRF on admin actions
**Threat**: Forged cross-site requests that trigger Approve/Unapprove/ToggleAdmin without the user's knowledge.  
**How to test**: Craft a form on an external page that POSTs to `/Users/Approve?id=<victim-id>` and load it while logged in as admin. The form lacks the `__RequestVerificationToken` field.  
**Expected**: 400 Bad Request (`ValidateAntiForgeryToken` rejects the request).  
**Failure indicator**: 302 redirect to Index (action executed).

#### T6A-07 — IDOR on Approve/Unapprove/ToggleAdmin
**Threat**: Admin sets another admin's `IsAdmin=false` or unapproves a co-admin.  
**How to test**: Log in as Admin A; POST `ToggleAdmin` with `id=<Admin-B's-ID>`. Also POST `Unapprove` with `id=<Admin-B's-ID>`.  
**Expected**: ToggleAdmin succeeds (spec only prevents self-demotion, not cross-admin toggling — this is by design). Unapprove also succeeds. Document this is accepted behaviour: admins can manage each other.  
**Note**: If the deployment has exactly one admin, the admin can lock everyone out by unapproving all users. Accept as a known risk; the first-user remains the recovery path (factory reset via DB).

#### T6A-08 — Admin self-demotion via direct POST
**Threat**: Admin bypasses the UI check and POSTs `ToggleAdmin` with their own ID directly (e.g. via curl).  
**How to test**: Log in as admin; POST `/Users/ToggleAdmin` with `id=<own-ID>` and a valid CSRF token.  
**Expected**: Controller returns redirect to Index without changing the flag (the server-side check in `ToggleAdmin` compares `currentUser.Id == id` and skips the toggle).  
**Failure indicator**: `IsAdmin` becomes false for the current user after the POST.

#### T6A-09 — First-user race condition (concurrent registration)
**Threat**: Two users register simultaneously; both see `Users.Count() == 1` after their own CreateAsync and both get auto-admin.  
**How to test**: Use two browser sessions (or a tool) to submit the registration form at the same time to a fresh database.  
**Expected**: In practice SQLite serialises writes, so only one of the two will see Count==1. The second sees Count==2. Manual test; document the result.  
**Note**: This is an inherent limitation of the optimistic count check. On a fresh install only one administrator should exist, so the risk is low (SQLite single-writer model mitigates this). If PostgreSQL is adopted later, this should be revisited with a DB-level constraint.

#### T6A-10 — Pending approval page accessible without login
**Threat**: Anonymous user navigates to `/PendingApproval/Index` to scrape the page or bypass something.  
**How to test**: Log out, then GET `/PendingApproval/Index`.  
**Expected**: 302 redirect to the login page (`[Authorize]` on `PendingApprovalController` triggers the Identity redirect).  
**Failure indicator**: 200 response with the pending approval message (information disclosure, minor).

### Tools

- Browser DevTools / curl / Burp Suite Community for manual request crafting
- The existing `docker compose up` stack for a local test environment

### Pass Criteria

All T6A-01 through T6A-10 behave as documented in "Expected" above.  
Any "Failure indicator" outcome constitutes a finding that must be fixed before Phase 6A is considered production-ready.

---

## Valgfrie fremtidige planer

Features that have been considered but deprioritised or deferred indefinitely.

### Per-sak kommunikasjonskanal

Each case could get its own real-time channel (e.g. a dedicated WhatsApp group or in-app thread) where board members can discuss the case asynchronously. Open question: a board member may be involved in many cases simultaneously, which would create many channels. Deferred; revisit if users express this need explicitly.

### Arkivering av lukkede saker

Closed cases and their events could be moved into separate archive storage, keeping the live
database small. Attachments are stored as BLOBs in the database by design — they are an integral
part of the case documentation and are not moving to the filesystem — so archiving closed cases is
the durable answer to database growth, and therefore to backup size. Deferred for future
consideration; not designed or scheduled.

### Jotta-backup (alternativ til Google Drive)

Jotta is a Norwegian cloud storage service that supports WebDAV. As an alternative to the Google Drive OAuth integration in Phase 6B, backups could be pushed to Jotta via WebDAV — simpler to implement (no OAuth app registration, credential-based auth) but less universal. Deferred in favour of Google Drive; revisit if users prefer Jotta.