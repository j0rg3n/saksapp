using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyDataToCaseEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Skip if already migrated (idempotency guard)
            // Migrate MeetingCases → CaseEvents (Category='meeting') + MeetingEventLinks + CaseEventCases
            migrationBuilder.Sql(@"
                INSERT INTO CaseEvents (CreatedAt, Content, Category, IsDeleted, DeletedAt, DeletedByUserId)
                SELECT m.MeetingDate, '', 'meeting', mc.IsDeleted, mc.DeletedAt, mc.DeletedByUserId
                FROM MeetingCases mc
                JOIN Meetings m ON m.Id = mc.MeetingId
                ORDER BY mc.Id;
            ");

            migrationBuilder.Sql(@"
                WITH mcs AS (
                    SELECT mc.Id AS mcId, mc.MeetingId, mc.BoardCaseId, mc.AgendaOrder, mc.AgendaTextSnapshot,
                           mc.TidsfristOverrideDate, mc.TidsfristOverrideText, mc.Outcome, mc.IsDeleted,
                           ROW_NUMBER() OVER (ORDER BY mc.Id) AS rn
                    FROM MeetingCases mc
                ),
                ces AS (
                    SELECT ce.Id AS ceId, ROW_NUMBER() OVER (ORDER BY ce.Id) AS rn
                    FROM CaseEvents ce WHERE ce.Category = 'meeting'
                ),
                combined AS (
                    SELECT mcs.MeetingId, ces.ceId AS CaseEventId, mcs.AgendaOrder, mcs.AgendaTextSnapshot,
                           mcs.TidsfristOverrideDate, mcs.TidsfristOverrideText, mcs.Outcome, mcs.IsDeleted,
                           mcs.mcId
                    FROM mcs JOIN ces ON mcs.rn = ces.rn
                )
                INSERT INTO MeetingEventLinks (MeetingId, CaseEventId, AgendaOrder, AgendaTextSnapshot,
                    TidsfristOverrideDate, TidsfristOverrideText, OfficialNotes, DecisionText, FollowUpText, Outcome, IsEventuelt, IsDeleted)
                SELECT c.MeetingId, c.CaseEventId, c.AgendaOrder, c.AgendaTextSnapshot,
                       c.TidsfristOverrideDate, c.TidsfristOverrideText,
                       mmce.OfficialNotes, mmce.DecisionText, mmce.FollowUpText,
                       COALESCE(mmce.Outcome, c.Outcome),
                       0,
                       c.IsDeleted
                FROM combined c
                LEFT JOIN MeetingMinutesCaseEntries mmce ON mmce.MeetingCaseId = c.mcId AND NOT mmce.IsDeleted;
            ");

            migrationBuilder.Sql(@"
                WITH mcs AS (
                    SELECT mc.BoardCaseId, mc.IsDeleted,
                           ROW_NUMBER() OVER (ORDER BY mc.Id) AS rn
                    FROM MeetingCases mc
                ),
                ces AS (
                    SELECT ce.Id AS ceId, ROW_NUMBER() OVER (ORDER BY ce.Id) AS rn
                    FROM CaseEvents ce WHERE ce.Category = 'meeting'
                )
                INSERT INTO CaseEventCases (CaseEventId, BoardCaseId, IsDeleted)
                SELECT ces.ceId, mcs.BoardCaseId, mcs.IsDeleted
                FROM mcs JOIN ces ON mcs.rn = ces.rn;
            ");

            // Migrate CaseComments → CaseEvents (Category='comment') + CaseEventCases
            migrationBuilder.Sql(@"
                INSERT INTO CaseEvents (CreatedAt, CreatedByUserId, Content, Category, IsDeleted, DeletedAt, DeletedByUserId)
                SELECT CreatedAt, CreatedByUserId, Text, 'comment', IsDeleted, DeletedAt, DeletedByUserId
                FROM CaseComments ORDER BY Id;
            ");

            migrationBuilder.Sql(@"
                WITH ccs AS (
                    SELECT BoardCaseId, IsDeleted,
                           ROW_NUMBER() OVER (ORDER BY Id) AS rn
                    FROM CaseComments
                ),
                ces AS (
                    SELECT Id AS ceId, ROW_NUMBER() OVER (ORDER BY Id) AS rn
                    FROM CaseEvents WHERE Category = 'comment'
                )
                INSERT INTO CaseEventCases (CaseEventId, BoardCaseId, IsDeleted)
                SELECT ces.ceId, ccs.BoardCaseId, ccs.IsDeleted
                FROM ccs JOIN ces ON ccs.rn = ces.rn;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM CaseEventCases;");
            migrationBuilder.Sql("DELETE FROM MeetingEventLinks;");
            migrationBuilder.Sql("DELETE FROM CaseEvents;");
        }
    }
}
