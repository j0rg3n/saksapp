using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceText = table.Column<string>(type: "TEXT", nullable: true),
                    AbsenceText = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovalOfPreviousMinutesText = table.Column<string>(type: "TEXT", nullable: true),
                    NextMeetingDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EventueltText = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutesAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesAttachments_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesCaseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingCaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardCaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    OfficialNotes = table.Column<string>(type: "TEXT", nullable: true),
                    DecisionText = table.Column<string>(type: "TEXT", nullable: true),
                    FollowUpText = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutesCaseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesCaseEntries_BoardCases_BoardCaseId",
                        column: x => x.BoardCaseId,
                        principalTable: "BoardCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesCaseEntries_MeetingCases_MeetingCaseId",
                        column: x => x.MeetingCaseId,
                        principalTable: "MeetingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesCaseEntries_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutesCaseEntryAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingMinutesCaseEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutesCaseEntryAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesCaseEntryAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutesCaseEntryAttachments_MeetingMinutesCaseEntries_MeetingMinutesCaseEntryId",
                        column: x => x.MeetingMinutesCaseEntryId,
                        principalTable: "MeetingMinutesCaseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_MeetingId",
                table: "MeetingMinutes",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesAttachments_AttachmentId",
                table: "MeetingMinutesAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesAttachments_MeetingId_AttachmentId",
                table: "MeetingMinutesAttachments",
                columns: new[] { "MeetingId", "AttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesCaseEntries_BoardCaseId",
                table: "MeetingMinutesCaseEntries",
                column: "BoardCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesCaseEntries_MeetingCaseId",
                table: "MeetingMinutesCaseEntries",
                column: "MeetingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesCaseEntries_MeetingId_MeetingCaseId",
                table: "MeetingMinutesCaseEntries",
                columns: new[] { "MeetingId", "MeetingCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesCaseEntryAttachments_AttachmentId",
                table: "MeetingMinutesCaseEntryAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutesCaseEntryAttachments_MeetingMinutesCaseEntryId_AttachmentId",
                table: "MeetingMinutesCaseEntryAttachments",
                columns: new[] { "MeetingMinutesCaseEntryId", "AttachmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingMinutes");

            migrationBuilder.DropTable(
                name: "MeetingMinutesAttachments");

            migrationBuilder.DropTable(
                name: "MeetingMinutesCaseEntryAttachments");

            migrationBuilder.DropTable(
                name: "MeetingMinutesCaseEntries");
        }
    }
}
