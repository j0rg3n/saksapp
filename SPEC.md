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

### Board Log (Styrelogg)

**Overview**
- A chronological log of events and activities related to board operations
- Events can exist independently or be linked to specific cases
- Multiple categories for different types of events

**Event Management**
- Create events with category (deviation/avvik, measure/tiltak, general, etc.)
- Link events to one or more cases
- Create events without linking to a case
- Meeting notes are treated as events with special types: "referat" (minutes), "vedtak" (decision), "oppfølging" (follow-up)

**WhatsApp Integration** (future)
- SaksApp bot for WhatsApp group
- Create events directly from WhatsApp
- Two-way sync: WhatsApp messages to log and vice versa
- Messages tagged with WhatsApp ID, updated on edit
- If edit fails, post new version as reply with link to old

### HMS Deviation and Measures Log

**Overview**
- Dedicated view for deviations and measures (Helse, Miljø, Sikkerhet)
- Shows all events categorized as deviations and measures
- Can reference related cases
- Typically does not include meetings (decisions not made there)

### PDF Generation

**Unified visual style**

Compact. Use font sizes, styles, and spacing to visually distinguish different sections and speed up visual search.

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

### Entities
- **BoardCase** - Case information
- **CaseComment** - Comments on a case
- **Attachment** - File attachments
- **Meeting** - Meeting metadata
- **MeetingCase** - Agenda item (join table between Meeting and BoardCase)
- **MeetingMinutes** - Minutes for a meeting
- **MeetingMinutesCaseEntry** - Per-agenda-item minutes
- **PdfGeneration** - Log of generated PDFs
- **BoardEvent** - Event/log entry in the board log (future)
- **BoardEventCase** - Many-to-many link between events and cases (future)

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