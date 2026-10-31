using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class QuestionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionProgresses",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Algorithm = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Due = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: false),
                    Difficulty = table.Column<double>(type: "double precision", nullable: false),
                    ScheduledDays = table.Column<int>(type: "integer", nullable: false),
                    LearningSteps = table.Column<int>(type: "integer", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    LastReviewAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionProgresses", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_QuestionProgresses_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ElapsedDays = table.Column<double>(type: "double precision", nullable: false),
                    PrevState = table.Column<int>(type: "integer", nullable: false),
                    PrevDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrevStability = table.Column<double>(type: "double precision", nullable: false),
                    PrevDifficulty = table.Column<double>(type: "double precision", nullable: false),
                    PrevScheduledDays = table.Column<int>(type: "integer", nullable: false),
                    PrevLearningSteps = table.Column<int>(type: "integer", nullable: false),
                    PrevReps = table.Column<int>(type: "integer", nullable: false),
                    PrevLapses = table.Column<int>(type: "integer", nullable: false),
                    PrevLastReviewAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionReviews_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionProgresses_UserId_Due",
                table: "QuestionProgresses",
                columns: new[] { "UserId", "Due" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviews_AttemptId_QuestionId",
                table: "QuestionReviews",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true,
                filter: "\"AttemptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviews_QuestionId_ReviewedAt",
                table: "QuestionReviews",
                columns: new[] { "QuestionId", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionProgresses");

            migrationBuilder.DropTable(
                name: "QuestionReviews");
        }
    }
}
