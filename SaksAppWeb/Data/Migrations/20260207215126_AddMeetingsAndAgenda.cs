using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingsAndAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseCommentAttachments_Attachments_AttachmentId",
                table: "CaseCommentAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CaseCommentAttachments_CaseComments_CaseCommentId",
                table: "CaseCommentAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CaseComments_BoardCases_BoardCaseId",
                table: "CaseComments");

            migrationBuilder.DropIndex(
                name: "IX_CaseComments_BoardCaseId_CreatedAt",
                table: "CaseComments");

            migrationBuilder.DropIndex(
                name: "IX_CaseCommentAttachments_CaseCommentId_AttachmentId",
                table: "CaseCommentAttachments");

            migrationBuilder.DropIndex(
                name: "IX_BoardCases_CaseNumber",
                table: "BoardCases");

            migrationBuilder.DropIndex(
                name: "IX_BoardCases_IsDeleted_Status",
                table: "BoardCases");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_EntityType_EntityId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents");

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    YearSequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardCaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgendaOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AgendaPointNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    AgendaTextSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    TidsfristOverrideDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    TidsfristOverrideText = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    FollowUpTextDraft = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingCases_BoardCases_BoardCaseId",
                        column: x => x.BoardCaseId,
                        principalTable: "BoardCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingCases_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_BoardCaseId",
                table: "CaseComments",
                column: "BoardCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseCommentAttachments_CaseCommentId",
                table: "CaseCommentAttachments",
                column: "CaseCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_BoardCaseId",
                table: "MeetingCases",
                column: "BoardCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_MeetingId_AgendaOrder",
                table: "MeetingCases",
                columns: new[] { "MeetingId", "AgendaOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_MeetingId_BoardCaseId",
                table: "MeetingCases",
                columns: new[] { "MeetingId", "BoardCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingDate",
                table: "Meetings",
                column: "MeetingDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_Year_YearSequenceNumber",
                table: "Meetings",
                columns: new[] { "Year", "YearSequenceNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseCommentAttachments_Attachments_AttachmentId",
                table: "CaseCommentAttachments",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseCommentAttachments_CaseComments_CaseCommentId",
                table: "CaseCommentAttachments",
                column: "CaseCommentId",
                principalTable: "CaseComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseComments_BoardCases_BoardCaseId",
                table: "CaseComments",
                column: "BoardCaseId",
                principalTable: "BoardCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseCommentAttachments_Attachments_AttachmentId",
                table: "CaseCommentAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CaseCommentAttachments_CaseComments_CaseCommentId",
                table: "CaseCommentAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CaseComments_BoardCases_BoardCaseId",
                table: "CaseComments");

            migrationBuilder.DropTable(
                name: "MeetingCases");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_CaseComments_BoardCaseId",
                table: "CaseComments");

            migrationBuilder.DropIndex(
                name: "IX_CaseCommentAttachments_CaseCommentId",
                table: "CaseCommentAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_BoardCaseId_CreatedAt",
                table: "CaseComments",
                columns: new[] { "BoardCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseCommentAttachments_CaseCommentId_AttachmentId",
                table: "CaseCommentAttachments",
                columns: new[] { "CaseCommentId", "AttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardCases_CaseNumber",
                table: "BoardCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardCases_IsDeleted_Status",
                table: "BoardCases",
                columns: new[] { "IsDeleted", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseCommentAttachments_Attachments_AttachmentId",
                table: "CaseCommentAttachments",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseCommentAttachments_CaseComments_CaseCommentId",
                table: "CaseCommentAttachments",
                column: "CaseCommentId",
                principalTable: "CaseComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseComments_BoardCases_BoardCaseId",
                table: "CaseComments",
                column: "BoardCaseId",
                principalTable: "BoardCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
