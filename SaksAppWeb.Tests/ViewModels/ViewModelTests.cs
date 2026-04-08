using Xunit;
using SaksAppWeb.Models;
using SaksAppWeb.Models.ViewModels;

namespace SaksAppWeb.Tests.ViewModels;

public class CaseIndexRowVmTests
{
    [Fact]
    public void CaseIndexRowVm_MapsCorrectly()
    {
        // Arrange
        var boardCase = new BoardCase
        {
            Id = 1,
            CaseNumber = 42,
            Title = "Test Case",
            Priority = CasePriority.High,
            Status = CaseStatus.Open,
            AssigneeUserId = "user-123",
            CustomTidsfristDate = new DateOnly(2026, 5, 1),
            CustomTidsfristText = "Urgent",
            Theme = "Finance"
        };

        // Act
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

        // Assert
        Assert.Equal(1, vm.Id);
        Assert.Equal(42, vm.CaseNumber);
        Assert.Equal("Test Case", vm.Title);
        Assert.Equal(CasePriority.High, vm.Priority);
        Assert.Equal(CaseStatus.Open, vm.Status);
        Assert.Equal("user-123", vm.AssigneeUserId);
        Assert.Equal("Test User", vm.AssigneeDisplay);
        Assert.Equal(new DateOnly(2026, 5, 1), vm.CustomTidsfristDate);
        Assert.Equal("Urgent", vm.CustomTidsfristText);
        Assert.Equal("Finance", vm.Theme);
    }

    [Theory]
    [InlineData(CasePriority.Low, 1)]
    [InlineData(CasePriority.Medium, 2)]
    [InlineData(CasePriority.High, 3)]
    public void CaseIndexRowVm_PriorityValues(CasePriority priority, int expectedValue)
    {
        // Act
        var vm = new CaseIndexRowVm { Priority = priority };

        // Assert
        Assert.Equal(priority, vm.Priority);
    }
}

public class MeetingMinutesVmTests
{
    [Fact]
    public void MeetingMinutesVm_InitializesCorrectly()
    {
        // Act
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
            EventueltText = "Ingen eventuelt"
        };

        // Assert
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
    }
}