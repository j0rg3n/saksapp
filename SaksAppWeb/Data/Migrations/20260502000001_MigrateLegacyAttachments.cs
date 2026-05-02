using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate CaseCommentAttachments → CaseEventAttachments
            // Correlate CaseComments → CaseEvents (category='comment') by ROW_NUMBER, same order as data migration
            migrationBuilder.Sql(@"
                WITH ccs AS (
                    SELECT cc.Id AS ccId, ROW_NUMBER() OVER (ORDER BY cc.Id) AS rn
                    FROM CaseComments cc
                ),
                ces AS (
                    SELECT ce.Id AS ceId, ROW_NUMBER() OVER (ORDER BY ce.Id) AS rn
                    FROM CaseEvents ce WHERE ce.Category = 'comment'
                ),
                mapping AS (
                    SELECT ccs.ccId, ces.ceId
                    FROM ccs JOIN ces ON ccs.rn = ces.rn
                )
                INSERT INTO CaseEventAttachments (CaseEventId, AttachmentId, IsDeleted, DeletedAt, DeletedByUserId)
                SELECT m.ceId, cca.AttachmentId, cca.IsDeleted, cca.DeletedAt, cca.DeletedByUserId
                FROM CaseCommentAttachments cca
                JOIN mapping m ON m.ccId = cca.CaseCommentId;
            ");

            // Migrate MeetingMinutesCaseEntryAttachments → CaseEventAttachments
            // Join via MeetingMinutesCaseEntries → MeetingEventLinks + CaseEventCases on (MeetingId, BoardCaseId)
            migrationBuilder.Sql(@"
                INSERT INTO CaseEventAttachments (CaseEventId, AttachmentId, IsDeleted, DeletedAt, DeletedByUserId)
                SELECT mel.CaseEventId, mmcea.AttachmentId, mmcea.IsDeleted, mmcea.DeletedAt, mmcea.DeletedByUserId
                FROM MeetingMinutesCaseEntryAttachments mmcea
                JOIN MeetingMinutesCaseEntries mmce ON mmce.Id = mmcea.MeetingMinutesCaseEntryId
                JOIN MeetingEventLinks mel ON mel.MeetingId = mmce.MeetingId
                JOIN CaseEventCases cec ON cec.CaseEventId = mel.CaseEventId AND cec.BoardCaseId = mmce.BoardCaseId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM CaseEventAttachments;");
        }
    }
}
