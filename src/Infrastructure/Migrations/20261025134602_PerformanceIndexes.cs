using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tags_UserId",
                table: "Tags",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_UserId_AnsweredAt",
                table: "Results",
                columns: new[] { "UserId", "AnsweredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_DeletedAt",
                table: "Questions",
                column: "DeletedAt",
                filter: "\"DeletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_UserId",
                table: "Questions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSessions_Unfinished",
                table: "FlashcardSessions",
                column: "StartedAt",
                filter: "\"FinishedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_AttemptStatus_StartedAt",
                table: "Attempts",
                columns: new[] { "AttemptStatus", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_UserId_AttemptStatus_FinishedAt",
                table: "Attempts",
                columns: new[] { "UserId", "AttemptStatus", "FinishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_UserId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Results_UserId_AnsweredAt",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_Questions_DeletedAt",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_UserId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_FlashcardSessions_Unfinished",
                table: "FlashcardSessions");

            migrationBuilder.DropIndex(
                name: "IX_Attempts_AttemptStatus_StartedAt",
                table: "Attempts");

            migrationBuilder.DropIndex(
                name: "IX_Attempts_UserId_AttemptStatus_FinishedAt",
                table: "Attempts");
        }
    }
}
