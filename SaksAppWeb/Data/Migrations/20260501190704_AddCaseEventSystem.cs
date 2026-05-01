using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseEventSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SourceGroupId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SourceSenderId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseEventAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEventAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseEventAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseEventAttachments_CaseEvents_CaseEventId",
                        column: x => x.CaseEventId,
                        principalTable: "CaseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseEventCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardCaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEventCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseEventCases_BoardCases_BoardCaseId",
                        column: x => x.BoardCaseId,
                        principalTable: "BoardCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseEventCases_CaseEvents_CaseEventId",
                        column: x => x.CaseEventId,
                        principalTable: "CaseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingEventLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CaseEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgendaOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AgendaTextSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    TidsfristOverrideDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    TidsfristOverrideText = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    OfficialNotes = table.Column<string>(type: "TEXT", nullable: true),
                    DecisionText = table.Column<string>(type: "TEXT", nullable: true),
                    FollowUpText = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEventuelt = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingEventLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingEventLinks_CaseEvents_CaseEventId",
                        column: x => x.CaseEventId,
                        principalTable: "CaseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingEventLinks_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventAttachments_AttachmentId",
                table: "CaseEventAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventAttachments_CaseEventId_AttachmentId",
                table: "CaseEventAttachments",
                columns: new[] { "CaseEventId", "AttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventCases_BoardCaseId",
                table: "CaseEventCases",
                column: "BoardCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventCases_CaseEventId_BoardCaseId",
                table: "CaseEventCases",
                columns: new[] { "CaseEventId", "BoardCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingEventLinks_CaseEventId",
                table: "MeetingEventLinks",
                column: "CaseEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingEventLinks_MeetingId_CaseEventId",
                table: "MeetingEventLinks",
                columns: new[] { "MeetingId", "CaseEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseEventAttachments");

            migrationBuilder.DropTable(
                name: "CaseEventCases");

            migrationBuilder.DropTable(
                name: "MeetingEventLinks");

            migrationBuilder.DropTable(
                name: "CaseEvents");
        }
    }
}
