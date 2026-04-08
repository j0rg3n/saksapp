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
- List of comments
- List of attachments
- Meeting history (when case was discussed, decisions made)

**Add Comment**
- Add text comment to a case
- Optionally attach files

**Add Attachment**
- Upload files (max 10MB)
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
  - `CaseId` - Optional (can be null for standalone board events)
  - `CreatedAt` - Timestamp (unique, single occurrence)
  - `Content` - Text content
  - `Category` - Event category (comment, general, avvik, tiltak, etc.)
  - `Attachments` - Associated files

- **MeetingEventLink** - Links CaseEvents to meetings with meeting-specific metadata
  - `MeetingId` - The meeting this event is associated with
  - `CaseEventId` - The linked event
  - `AgendaOrder` - Index of the case in the meeting agenda
  - `OfficialNotes` - Referat (meeting minutes notes)
  - `DecisionText` - Vedtak (decision)
  - `FollowUpText` - Oppfølging (follow-up)

**Notes:**
- A CaseEvent has a single timestamp and cannot appear in multiple meetings
- Meeting-specific outcomes (referat, vedtak, oppfølging) are stored on MeetingEventLink, not on CaseEvent
- MeetingMinutes remains separate for meeting header information (date, approval status)

**Migration:**
- CaseComment → CaseEvent (Category = "comment")
- MeetingMinutesCaseEntry → Up to 3 CaseEvents (one per non-empty field), linked via MeetingEventLink

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

### Testing

**Coverage Target**
- Minimum 75% code coverage for business logic
- Focus on services, controllers, and data transformation logic

**Mocking Strategy**
To achieve the coverage target, the following external services must be mockable:
- **Database**: Entity Framework DbContext - use in-memory provider or mocking libraries
- **Email (SMTP)**: SmtpEmailSender - mock IEmailSender interface
- **PDF Generation**: SimplePdfWriter - mock or use test implementation
- **User Manager**: ASP.NET Identity UserManager - mock for authentication tests
- **File System**: Data protection keys, database files - use temporary directories
- **Audit Service**: IAuditService - mock to capture logged events

**Test Organization**
- Unit tests for isolated logic
- Integration tests for database operations
- Controller tests for HTTP request/response handling