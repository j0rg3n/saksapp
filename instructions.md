# SaksApp - Project Documentation

## Project Overview

SaksApp is a Norwegian board/case management system built with ASP.NET Core 8.0. It's designed to help organizations manage board cases (saker), schedule meetings, create agendas, generate minutes, and maintain an audit trail of all activities.

### Core Functionality

- **Case Management**: Create, edit, and track board cases with assignees, priorities, deadlines, and comments
- **Meeting Management**: Schedule meetings, build agendas from cases, and generate PDF documents
- **Minutes & Documentation**: Record meeting minutes, decisions, and follow-up actions
- **File Attachments**: Support for PDFs and images attached to cases, comments, and minutes
- **Audit Logging**: Complete audit trail of all system activities
- **User Authentication**: Integration with ASP.NET Core Identity for user management

## Technology Stack

- **.NET 8.0** with ASP.NET Core MVC
- **Entity Framework Core** with SQLite database
- **ASP.NET Core Identity** for authentication
- **Bootstrap 5** for UI styling
- **PdfSharpCore** for PDF generation
- **HtmlAgilityPack** for HTML processing
- **Docker** support with compose.yaml

## Architecture

### Project Structure
```
SaksApp/
├── SaksAppWeb/                 # Main web application
│   ├── Controllers/           # MVC Controllers
│   ├── Data/                  # Entity Framework DbContext & Migrations
│   ├── Models/                # Domain models and ViewModels
│   ├── Services/              # Business logic services
│   ├── Views/                 # Razor views
│   └── wwwroot/              # Static files
├── compose.yaml               # Docker configuration
└── SaksApp.sln               # Solution file
```

### Key Components

#### Controllers
- `HomeController`: Main dashboard
- `CasesController`: Case CRUD operations, comments, attachments
- `MeetingsController`: Meeting management, agenda, minutes, PDF generation
- `AuditEventController`: Audit log viewing
- `HtmlCaseImportController`: Import cases from HTML

#### Services (Dependency Injection)
- `IAuditService`: Audit logging implementation
- `ICaseNumberAllocator`: Automatic case number assignment
- `IPdfSequenceService`: PDF version numbering
- `HtmlCaseImporter`: HTML parsing for case imports

#### Domain Models
- `BoardCase`: Main case entity with status, priority, assignee
- `Meeting`: Meeting with date, location, sequence numbering
- `MeetingCase`: Junction entity for cases in meeting agendas
- `MeetingMinutes`: Meeting minutes and attendance information
- `Attachment`: File storage for documents and images
- `AuditEvent`: Audit trail entries

## Code Style & Conventions

### C# Coding Standards

#### Naming Conventions
- **Classes**: PascalCase (e.g., `BoardCase`, `CasesController`)
- **Methods**: PascalCase (e.g., `CreateAsync`, `PopulateAssignees`)
- **Properties**: PascalCase (e.g., `CaseNumber`, `CustomTidsfristDate`)
- **Fields**: _camelCase with underscore prefix (e.g., `_db`, `_userManager`)
- **Constants**: PascalCase (e.g., `MaxUploadBytes`)
- **Interfaces**: PascalCase with 'I' prefix (e.g., `IAuditService`)

#### File Organization
- One class per file
- File name matches class name
- Models organized by domain (`BoardCaseModel.cs`, `MeetingModel.cs`)
- ViewModels in separate folder (`ViewModels/`)
- Enums in separate files (`CaseStatusEnum.cs`, `AuditActionEnum.cs`)

#### Code Patterns
- **Async/Await**: All database operations use async patterns with `CancellationToken`
- **Dependency Injection**: Constructor injection with private readonly fields
- **Null Safety**: Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **Using Statements**: Static imports for common namespaces (`using Microsoft.EntityFrameworkCore;`)
- **Method Length**: Controllers methods should be concise, delegate complex logic to services

#### Entity Framework Patterns
- **Query Filters**: Soft delete filters applied in `OnModelCreating`
- **No Tracking**: Read-only queries use `AsNoTracking()`
- **Includes**: Eager loading with explicit `Include()` calls
- **Transaction Safety**: Related entities created/updated in same SaveChanges call

### UI/UX Conventions

#### Razor View Patterns
- **Bootstrap 5**: Responsive design with Bootstrap components
- **Tag Helpers**: Extensive use of ASP.NET Core tag helpers (`asp-for`, `asp-action`)
- **Validation**: Client-side and server-side validation with Bootstrap styling
- **Navigation**: Consistent navbar with main sections (Home, Cases, Meetings, Audit, Import)

#### Norwegian Language Support
- UI text uses Norwegian terms ("Saker", "Møter", "Vedlegg")
- Property names mix English (code) with Norwegian concepts (`TidsfristDate`)
- Error messages and user-facing text should be in Norwegian

#### CSS/Styling
- Bootstrap utilities for spacing and layout (`mb-3`, `container-fluid`)
- Custom CSS in `site.css` for application-specific styling
- Responsive design with mobile-first approach

## Database Design

### Key Concepts
- **Soft Deletes**: All entities inherit from `SoftDeletableEntity`
- **Case Numbers**: Automatically assigned sequential numbers
- **Meeting Sequencing**: Year-based sequence numbers (e.g., 2024/1, 2024/2)
- **PDF Versioning**: Sequential PDF versions per document type

### Important Tables
- `BoardCases`: Main case records
- `Meetings`: Meeting information with year/sequence
- `MeetingCases`: Agenda items linking cases to meetings
- `Attachments`: Binary file storage
- `AuditEvents`: Complete audit trail

## Development Guidelines

### When Adding New Features

1. **Create Models First**: Define domain entities with proper validation attributes
2. **Add Database Migrations**: Use EF Core migrations for schema changes
3. **Implement Services**: Put business logic in injectable services
4. **Controller Actions**: Follow RESTful patterns (Index, Details, Create, Edit)
5. **Add Audit Logging**: Log all CRUD operations using `IAuditService`
6. **Write Views**: Follow existing view patterns and Bootstrap styling
7. **Test Navigation**: Ensure all links work and navigation is consistent

### Security Considerations
- All controllers require `[Authorize]` except Home/Privacy
- File upload validation (size limits, content type checking)
- SQL injection prevention through parameterized queries
- CSRF protection with `[ValidateAntiForgeryToken]`
- Input validation through DataAnnotations

### Performance Considerations
- Use `AsNoTracking()` for read-only queries
- Implement pagination for large datasets
- Optimize EF Core queries to prevent N+1 problems
- Cache frequently accessed reference data

## Important Implementation Details

### PDF Generation
- Uses `SimplePdfWriterService` for agenda, minutes, and reminder PDFs
- PDF version numbering prevents conflicts
- Includes previous meeting follow-ups and between-meeting comments
- Supports attachment references in PDFs

### File Handling
- Maximum file sizes: 10MB for general uploads, 20MB for signed minutes
- Allowed types: PDF and common image formats
- Files stored as binary data in SQLite database
- Soft delete applied to attachments when removed

### Audit System
- Comprehensive logging of all user actions
- Records before/after state for updates
- Includes user information and timestamps
- Can be filtered by entity type and action

### Norwegian Context
- "Sak" = Case/Issue
- "Tidsfrist" = Deadline
- "Referat" = Meeting minutes
- "Vedtak" = Decision/Resolution
- "Oppfølging" = Follow-up

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- SQLite (included with EF Core)

### Running the Application
1. Clone repository
2. Navigate to `SaksApp/SaksAppWeb`
3. Run `dotnet ef database update` to apply migrations
4. Run `dotnet run` to start the application
5. Access at `https://localhost:7xxx`

### Docker Support
- Use `compose.yaml` for containerized deployment
- Multi-stage Dockerfile included
- Targets Linux containers

## Testing Strategy

### Manual Testing Areas
- Case creation/editing workflow
- Meeting agenda building and PDF generation
- File upload/download functionality
- Audit log completeness
- User authentication and authorization

### Key Test Scenarios
- Complete case lifecycle (creation → comments → meeting → resolution)
- PDF generation for all document types
- File attachment workflows
- Audit trail verification
- Error handling (invalid data, file size limits)

## Maintenance Notes

### Regular Tasks
- Monitor database size (especially with file attachments)
- Review audit logs for unusual activity
- Backup SQLite database regularly
- Update dependencies (especially security patches)

### Common Issues
- File upload size limits may need adjustment based on usage
- PDF generation performance with many attachments
- Database cleanup for soft-deleted records
- User permission management

### Future Enhancements
- Email notifications for deadlines/assignments
- Full-text search for cases and comments
- External file storage (Azure Blob, S3)
- Reporting/analytics dashboards
- Mobile-responsive improvements