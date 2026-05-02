using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaksAppWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilteredUniqueIndexCaseEventCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseEventCases_CaseEventId_BoardCaseId",
                table: "CaseEventCases");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventCases_CaseEventId_BoardCaseId",
                table: "CaseEventCases",
                columns: new[] { "CaseEventId", "BoardCaseId" },
                unique: true,
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseEventCases_CaseEventId_BoardCaseId",
                table: "CaseEventCases");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEventCases_CaseEventId_BoardCaseId",
                table: "CaseEventCases",
                columns: new[] { "CaseEventId", "BoardCaseId" },
                unique: true);
        }
    }
}
