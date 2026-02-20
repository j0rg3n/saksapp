using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfGenerationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingCases_BoardCases_BoardCaseId",
                table: "MeetingCases");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingCases_Meetings_MeetingId",
                table: "MeetingCases");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_MeetingDate",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_Year_YearSequenceNumber",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_MeetingCases_MeetingId_AgendaOrder",
                table: "MeetingCases");

            migrationBuilder.DropIndex(
                name: "IX_MeetingCases_MeetingId_BoardCaseId",
                table: "MeetingCases");

            migrationBuilder.CreateTable(
                name: "PdfGenerations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GeneratedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfGenerations_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_MeetingId",
                table: "MeetingCases",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfGenerations_MeetingId_DocumentType",
                table: "PdfGenerations",
                columns: new[] { "MeetingId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfGenerations_MeetingId_DocumentType_SequenceNumber",
                table: "PdfGenerations",
                columns: new[] { "MeetingId", "DocumentType", "SequenceNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingCases_BoardCases_BoardCaseId",
                table: "MeetingCases",
                column: "BoardCaseId",
                principalTable: "BoardCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingCases_Meetings_MeetingId",
                table: "MeetingCases",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingCases_BoardCases_BoardCaseId",
                table: "MeetingCases");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingCases_Meetings_MeetingId",
                table: "MeetingCases");

            migrationBuilder.DropTable(
                name: "PdfGenerations");

            migrationBuilder.DropIndex(
                name: "IX_MeetingCases_MeetingId",
                table: "MeetingCases");

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

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_MeetingId_AgendaOrder",
                table: "MeetingCases",
                columns: new[] { "MeetingId", "AgendaOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCases_MeetingId_BoardCaseId",
                table: "MeetingCases",
                columns: new[] { "MeetingId", "BoardCaseId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingCases_BoardCases_BoardCaseId",
                table: "MeetingCases",
                column: "BoardCaseId",
                principalTable: "BoardCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingCases_Meetings_MeetingId",
                table: "MeetingCases",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
