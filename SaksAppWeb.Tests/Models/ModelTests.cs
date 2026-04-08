using Xunit;
using SaksAppWeb.Models;
using System;

namespace SaksAppWeb.Tests.Models;

public class BoardCaseTests
{
    [Fact]
    public void BoardCase_DefaultValues()
    {
        var boardCase = new BoardCase();

        Assert.Equal(0, boardCase.CaseNumber);
        Assert.Equal(string.Empty, boardCase.Title);
        Assert.Equal(CasePriority.P2, boardCase.Priority);
        Assert.Equal(CaseStatus.Open, boardCase.Status);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), boardCase.StartDate);
    }

    [Fact]
    public void BoardCase_CanSetAllProperties()
    {
        var boardCase = new BoardCase
        {
            Id = 1,
            CaseNumber = 42,
            Title = "Test Case",
            Description = "Description",
            Theme = "Theme",
            Priority = CasePriority.High,
            AssigneeUserId = "user-123",
            StartDate = new DateOnly(2026, 1, 1),
            Status = CaseStatus.Closed,
            ClosedDate = new DateOnly(2026, 3, 1),
            CustomTidsfristDate = new DateOnly(2026, 6, 1),
            CustomTidsfristText = "Tidsfrist"
        };

        Assert.Equal(1, boardCase.Id);
        Assert.Equal(42, boardCase.CaseNumber);
        Assert.Equal("Test Case", boardCase.Title);
        Assert.Equal("Description", boardCase.Description);
        Assert.Equal("Theme", boardCase.Theme);
        Assert.Equal(CasePriority.High, boardCase.Priority);
        Assert.Equal("user-123", boardCase.AssigneeUserId);
        Assert.Equal(new DateOnly(2026, 1, 1), boardCase.StartDate);
        Assert.Equal(CaseStatus.Closed, boardCase.Status);
        Assert.Equal(new DateOnly(2026, 3, 1), boardCase.ClosedDate);
        Assert.Equal(new DateOnly(2026, 6, 1), boardCase.CustomTidsfristDate);
        Assert.Equal("Tidsfrist", boardCase.CustomTidsfristText);
    }
}

public class MeetingTests
{
    [Fact]
    public void Meeting_DefaultValues()
    {
        var meeting = new Meeting();

        Assert.Equal(0, meeting.Id);
        Assert.Equal(0, meeting.Year);
        Assert.Equal(0, meeting.YearSequenceNumber);
        Assert.Null(meeting.Location);
    }

    [Fact]
    public void Meeting_CanSetAllProperties()
    {
        var meeting = new Meeting
        {
            Id = 1,
            MeetingDate = new DateOnly(2026, 4, 15),
            Year = 2026,
            YearSequenceNumber = 3,
            Location = "Oslo"
        };

        Assert.Equal(1, meeting.Id);
        Assert.Equal(new DateOnly(2026, 4, 15), meeting.MeetingDate);
        Assert.Equal(2026, meeting.Year);
        Assert.Equal(3, meeting.YearSequenceNumber);
        Assert.Equal("Oslo", meeting.Location);
    }
}

public class MeetingCaseTests
{
    [Fact]
    public void MeetingCase_DefaultValues()
    {
        var mc = new MeetingCase();

        Assert.Equal(0, mc.AgendaOrder);
        Assert.Equal(string.Empty, mc.AgendaTextSnapshot);
    }

    [Fact]
    public void MeetingCase_CanSetAllProperties()
    {
        var mc = new MeetingCase
        {
            Id = 1,
            MeetingId = 10,
            BoardCaseId = 20,
            AgendaOrder = 3,
            AgendaPointNumber = 1,
            AgendaTextSnapshot = "Agenda text",
            TidsfristOverrideDate = new DateOnly(2026, 6, 1),
            TidsfristOverrideText = "Override text",
            Outcome = MeetingCaseOutcome.Continue,
            FollowUpTextDraft = "Draft text"
        };

        Assert.Equal(1, mc.Id);
        Assert.Equal(10, mc.MeetingId);
        Assert.Equal(20, mc.BoardCaseId);
        Assert.Equal(3, mc.AgendaOrder);
        Assert.Equal(1, mc.AgendaPointNumber);
        Assert.Equal("Agenda text", mc.AgendaTextSnapshot);
        Assert.Equal(new DateOnly(2026, 6, 1), mc.TidsfristOverrideDate);
        Assert.Equal("Override text", mc.TidsfristOverrideText);
        Assert.Equal(MeetingCaseOutcome.Continue, mc.Outcome);
        Assert.Equal("Draft text", mc.FollowUpTextDraft);
    }
}

public class CaseCommentTests
{
    [Fact]
    public void CaseComment_DefaultValues()
    {
        var comment = new CaseComment();

        Assert.Equal(string.Empty, comment.Text);
        Assert.False(comment.IsDeleted);
    }

    [Fact]
    public void CaseComment_CanSetAllProperties()
    {
        var comment = new CaseComment
        {
            Id = 1,
            BoardCaseId = 10,
            Text = "Comment text",
            CreatedAt = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            DeletedAt = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero),
            DeletedByUserId = "user-123"
        };

        Assert.Equal(1, comment.Id);
        Assert.Equal(10, comment.BoardCaseId);
        Assert.Equal("Comment text", comment.Text);
        Assert.True(comment.IsDeleted);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero), comment.DeletedAt);
        Assert.Equal("user-123", comment.DeletedByUserId);
    }
}

public class MeetingMinutesTests
{
    [Fact]
    public void MeetingMinutes_DefaultValues()
    {
        var minutes = new MeetingMinutes();

        Assert.Equal(string.Empty, minutes.AttendanceText);
    }

    [Fact]
    public void MeetingMinutes_CanSetAllProperties()
    {
        var minutes = new MeetingMinutes
        {
            Id = 1,
            MeetingId = 10,
            AttendanceText = "Hansen, Johansen",
            AbsenceText = "Olsen",
            ApprovalOfPreviousMinutesText = "Godkjent",
            NextMeetingDate = new DateOnly(2026, 5, 1),
            EventueltText = "Eventuelt text"
        };

        Assert.Equal(1, minutes.Id);
        Assert.Equal(10, minutes.MeetingId);
        Assert.Equal("Hansen, Johansen", minutes.AttendanceText);
        Assert.Equal("Olsen", minutes.AbsenceText);
        Assert.Equal("Godkjent", minutes.ApprovalOfPreviousMinutesText);
        Assert.Equal(new DateOnly(2026, 5, 1), minutes.NextMeetingDate);
        Assert.Equal("Eventuelt text", minutes.EventueltText);
    }
}

public class AttachmentTests
{
    [Fact]
    public void Attachment_DefaultValues()
    {
        var attachment = new Attachment();

        Assert.Equal(string.Empty, attachment.OriginalFileName);
    }

    [Fact]
    public void Attachment_CanSetAllProperties()
    {
        var attachment = new Attachment
        {
            Id = 1,
            OriginalFileName = "document.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Content = new byte[] { 1, 2, 3 }
        };

        Assert.Equal(1, attachment.Id);
        Assert.Equal("document.pdf", attachment.OriginalFileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1024, attachment.SizeBytes);
    }
}

public class SoftDeletableEntityTests
{
    [Fact]
    public void SoftDeletableEntity_DefaultIsDeletedFalse()
    {
        var entity = new TestSoftDeletableEntity();
        Assert.False(entity.IsDeleted);
    }

    private class TestSoftDeletableEntity : SoftDeletableEntity { }
}

public class MeetingMinutesCaseEntryTests
{
    [Fact]
    public void MeetingMinutesCaseEntry_CanSetAllProperties()
    {
        var entry = new MeetingMinutesCaseEntry
        {
            Id = 1,
            MeetingId = 10,
            MeetingCaseId = 20,
            BoardCaseId = 30,
            OfficialNotes = "Official note",
            DecisionText = "Decision text",
            FollowUpText = "Follow up text",
            Outcome = MeetingCaseOutcome.Closed
        };

        Assert.Equal(1, entry.Id);
        Assert.Equal(10, entry.MeetingId);
        Assert.Equal(20, entry.MeetingCaseId);
        Assert.Equal(30, entry.BoardCaseId);
        Assert.Equal("Official note", entry.OfficialNotes);
        Assert.Equal("Decision text", entry.DecisionText);
        Assert.Equal("Follow up text", entry.FollowUpText);
        Assert.Equal(MeetingCaseOutcome.Closed, entry.Outcome);
    }
}