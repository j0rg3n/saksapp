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

### Meeting Management

**List Meetings**
- View all meetings, sorted by date (newest first)
- Displayed columns: Date, Year/Sequence, Location

**Create Meeting**
- Form with: MeetingDate, YearSequenceNumber, Location

**Edit Meeting**
- Modify meeting date, sequence number, and location

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
- Mark outcome (e.g., continued, decided, archived)
- Download minutes PDF

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

- Grouping window duration (minutes) — per group or global default (5 min).
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

### PDF Generation

**Unified visual style**

Compact. Use font sizes, styles, and spacing to visually distinguish different sections and speed up visual search.

**Layouts**: See [LAYOUTS.md](./LAYOUTS.md) for detailed visual mockups and specifications.

**Agenda PDF**
- For each agenda item: Title, CaseNumber, Assignee
- Case description (in italics)
- Agenda text snapshot (in regular text, if different from description)
- Tidsfrist
- Previous meeting follow-up (if applicable)
- Comments since last meeting
- Attachment references

**Minutes PDF**
- Meeting header (date, location)
- Attendance
- Approval of previous minutes
- For each agenda item: Title, CaseNumber, Decision, Follow-up, Attachments
- Eventuelt section
- Next meeting date

### User Management

**List Users**
- View all users with their full names and emails
- Edit user (full name, email)

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

### Data Protection
- ASP.NET Data Protection for antiforgery tokens
- Keys persisted to filesystem

### Background Services
- Database backup service - hourly backups using SQLite Online Backup API
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