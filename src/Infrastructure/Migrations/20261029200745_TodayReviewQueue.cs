using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class TodayReviewQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "QuestionReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DictionaryId",
                table: "FlashcardSessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "FlashcardSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuestionReviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionsTotal = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReviewSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviews_SessionId_QuestionId",
                table: "QuestionReviews",
                columns: new[] { "SessionId", "QuestionId" },
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewSessions_UserId_StartedAt",
                table: "QuestionReviewSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionReviews_QuestionReviewSessions_SessionId",
                table: "QuestionReviews",
                column: "SessionId",
                principalTable: "QuestionReviewSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionReviews_QuestionReviewSessions_SessionId",
                table: "QuestionReviews");

            migrationBuilder.DropTable(
                name: "QuestionReviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionReviews_SessionId_QuestionId",
                table: "QuestionReviews");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "QuestionReviews");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "FlashcardSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "DictionaryId",
                table: "FlashcardSessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
