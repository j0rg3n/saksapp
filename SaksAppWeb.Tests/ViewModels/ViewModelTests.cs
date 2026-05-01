using Xunit;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Tests.ViewModels;

public class CaseIndexRowVmTests
{
    [Fact]
    public void CaseIndexRowVm_MapsAllFields()
    {
        var boardCase = new BoardCase
        {
            Id = 1,
            CaseNumber = 42,
            Title = "Test Case",
            Priority = CasePriority.P3,
            Status = CaseStatus.Open,
            AssigneeUserId = "user-123",
            CustomTidsfristDate = new DateOnly(2026, 5, 1),
            CustomTidsfristText = "Urgent",
            Theme = "Finance"
        };

        var vm = new CaseIndexRowVm
        {
            Id = boardCase.Id,
            CaseNumber = boardCase.CaseNumber,
            Title = boardCase.Title,
            Priority = boardCase.Priority,
            Status = boardCase.Status,
            AssigneeUserId = boardCase.AssigneeUserId,
            AssigneeDisplay = "Test User",
            CustomTidsfristDate = boardCase.CustomTidsfristDate,
            CustomTidsfristText = boardCase.CustomTidsfristText,
            Theme = boardCase.Theme
        };

        Assert.Equal(1, vm.Id);
        Assert.Equal(42, vm.CaseNumber);
        Assert.Equal("Test Case", vm.Title);
        Assert.Equal(CasePriority.P3, vm.Priority);
        Assert.Equal(CaseStatus.Open, vm.Status);
        Assert.Equal("user-123", vm.AssigneeUserId);
        Assert.Equal("Test User", vm.AssigneeDisplay);
        Assert.Equal(new DateOnly(2026, 5, 1), vm.CustomTidsfristDate);
        Assert.Equal("Urgent", vm.CustomTidsfristText);
        Assert.Equal("Finance", vm.Theme);
    }

    [Theory]
    [InlineData(CasePriority.P1)]
    [InlineData(CasePriority.P2)]
    [InlineData(CasePriority.P3)]
    public void CaseIndexRowVm_PriorityValues(CasePriority priority)
    {
        var vm = new CaseIndexRowVm { Priority = priority };
        Assert.Equal(priority, vm.Priority);
    }

    [Theory]
    [InlineData(CaseStatus.Open)]
    [InlineData(CaseStatus.Closed)]
    public void CaseIndexRowVm_StatusValues(CaseStatus status)
    {
        var vm = new CaseIndexRowVm { Status = status };
        Assert.Equal(status, vm.Status);
    }
}

public class MeetingMinutesVmTests
{
    [Fact]
    public void MeetingMinutesVm_InitializesAllFields()
    {
        var vm = new MeetingMinutesVm
        {
            MeetingId = 1,
            MeetingDate = new DateOnly(2026, 4, 1),
            Year = 2026,
            YearSequenceNumber = 4,
            Location = "Oslo",
            AttendanceText = "Hansen, Johansen",
            AbsenceText = "Olsen",
            ApprovalOfPreviousMinutesText = "Godkjent",
            NextMeetingDate = new DateOnly(2026, 5, 1),
            EventueltText = "Ingen eventuelt",
            CaseEntries = new List<MeetingMinutesCaseEntryVm>
            {
                new() { BoardCaseId = 1, CaseNumber = 42, Title = "Test" }
            }
        };

        Assert.Equal(1, vm.MeetingId);
        Assert.Equal(new DateOnly(2026, 4, 1), vm.MeetingDate);
        Assert.Equal(2026, vm.Year);
        Assert.Equal(4, vm.YearSequenceNumber);
        Assert.Equal("Oslo", vm.Location);
        Assert.Equal("Hansen, Johansen", vm.AttendanceText);
        Assert.Equal("Olsen", vm.AbsenceText);
        Assert.Equal("Godkjent", vm.ApprovalOfPreviousMinutesText);
        Assert.Equal(new DateOnly(2026, 5, 1), vm.NextMeetingDate);
        Assert.Equal("Ingen eventuelt", vm.EventueltText);
        Assert.Single(vm.CaseEntries);
    }

    [Fact]
    public void MeetingMinutesVm_CanBeEmpty()
    {
        var vm = new MeetingMinutesVm();

        Assert.Equal(0, vm.MeetingId);
        Assert.Null(vm.Location);
        Assert.Empty(vm.CaseEntries);
    }
}

public class MeetingMinutesCaseEntryVmTests
{
    [Fact]
    public void MeetingMinutesCaseEntryVm_MapsFields()
    {
        var vm = new MeetingMinutesCaseEntryVm
        {
            MeetingEventLinkId = 1,
            BoardCaseId = 42,
            CaseNumber = 100,
            Title = "Test Case",
            AssigneeDisplay = "John Doe",
            OfficialNotes = "Note text",
            DecisionText = "Decision made",
            FollowUpText = "Follow up needed",
            Outcome = MeetingCaseOutcome.Continue
        };

        Assert.Equal(1, vm.MeetingEventLinkId);
        Assert.Equal(42, vm.BoardCaseId);
        Assert.Equal(100, vm.CaseNumber);
        Assert.Equal("Test Case", vm.Title);
        Assert.Equal("John Doe", vm.AssigneeDisplay);
        Assert.Equal("Note text", vm.OfficialNotes);
        Assert.Equal("Decision made", vm.DecisionText);
        Assert.Equal("Follow up needed", vm.FollowUpText);
        Assert.Equal(MeetingCaseOutcome.Continue, vm.Outcome);
    }
}

public class CaseEditVmTests
{
    [Fact]
    public void CaseEditVm_DefaultValues()
    {
        var vm = new CaseEditVm();

        Assert.Null(vm.Id);
        Assert.Equal(string.Empty, vm.Title);
        Assert.Equal(CasePriority.P2, vm.Priority);
        Assert.Equal(CaseStatus.Open, vm.Status);
    }

    [Fact]
    public void CaseEditVm_CanSetAllFields()
    {
        var vm = new CaseEditVm
        {
            Id = 1,
            Title = "Test Title",
            Description = "Test Description",
            Theme = "Theme",
            Priority = CasePriority.P3,
            Status = CaseStatus.Closed,
            AssigneeUserId = "user-123",
            StartDate = new DateOnly(2026, 1, 1),
            ClosedDate = new DateOnly(2026, 3, 1),
            CustomTidsfristDate = new DateOnly(2026, 6, 1),
            CustomTidsfristText = "Tidsfrist text"
        };

        Assert.Equal(1, vm.Id);
        Assert.Equal("Test Title", vm.Title);
        Assert.Equal("Test Description", vm.Description);
        Assert.Equal("Theme", vm.Theme);
        Assert.Equal(CasePriority.P3, vm.Priority);
        Assert.Equal(CaseStatus.Closed, vm.Status);
        Assert.Equal("user-123", vm.AssigneeUserId);
    }
}

public class MeetingEditVmTests
{
    [Fact]
    public void MeetingEditVm_DefaultValues()
    {
        var vm = new MeetingEditVm();

        Assert.Null(vm.Id);
        Assert.Equal(1, vm.YearSequenceNumber);
    }

    [Fact]
    public void MeetingEditVm_SetsDateProperties()
    {
        var vm = new MeetingEditVm
        {
            MeetingDate = new DateOnly(2026, 4, 15),
            YearSequenceNumber = 3,
            Location = "Oslo"
        };

        Assert.Equal(new DateOnly(2026, 4, 15), vm.MeetingDate);
        Assert.Equal(3, vm.YearSequenceNumber);
        Assert.Equal("Oslo", vm.Location);
    }
}

public class CaseDetailsVmTests
{
    [Fact]
    public void CaseDetailsVm_ContainsAllSections()
    {
        var vm = new CaseDetailsVm
        {
            Case = new BoardCase { Id = 1, CaseNumber = 42, Title = "Test", Status = CaseStatus.Open },
            Timeline = new List<CaseTimelineItemVm>()
        };

        Assert.NotNull(vm.Case);
        Assert.NotNull(vm.Timeline);
    }
}

public class CaseStatusEnumTests
{
    [Fact]
    public void CaseStatus_HasExpectedValues()
    {
        Assert.Equal(1, (int)CaseStatus.Open);
        Assert.Equal(2, (int)CaseStatus.OnHold);
        Assert.Equal(3, (int)CaseStatus.Closed);
    }
}

public class CasePriorityEnumTests
{
    [Fact]
    public void CasePriority_HasExpectedValues()
    {
        Assert.Equal(1, (int)CasePriority.P1);
        Assert.Equal(2, (int)CasePriority.P2);
        Assert.Equal(3, (int)CasePriority.P3);
    }
}

public class MeetingCaseOutcomeEnumTests
{
    [Theory]
    [InlineData(MeetingCaseOutcome.Continue)]
    [InlineData(MeetingCaseOutcome.Closed)]
    [InlineData(MeetingCaseOutcome.Deferred)]
    [InlineData(MeetingCaseOutcome.Orientering)]
    public void MeetingCaseOutcome_AllValuesWork(MeetingCaseOutcome outcome)
    {
        var vm = new MeetingMinutesCaseEntryVm { Outcome = outcome };
        Assert.Equal(outcome, vm.Outcome);
    }
}