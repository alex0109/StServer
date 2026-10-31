using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DictionaryEntryProgresses",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_DictionaryEntryProgresses", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_DictionaryEntryProgresses_DictionaryEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "DictionaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    CardsTotal = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardSessions_Dictionaries_DictionaryId",
                        column: x => x.DictionaryId,
                        principalTable: "Dictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_FlashcardReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardReviews_DictionaryEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "DictionaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardReviews_FlashcardSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "FlashcardSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryEntryProgresses_UserId_Due",
                table: "DictionaryEntryProgresses",
                columns: new[] { "UserId", "Due" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_EntryId_ReviewedAt",
                table: "FlashcardReviews",
                columns: new[] { "EntryId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_SessionId",
                table: "FlashcardReviews",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardReviews_UserId_ReviewedAt",
                table: "FlashcardReviews",
                columns: new[] { "UserId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSessions_DictionaryId_UserId",
                table: "FlashcardSessions",
                columns: new[] { "DictionaryId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DictionaryEntryProgresses");

            migrationBuilder.DropTable(
                name: "FlashcardReviews");

            migrationBuilder.DropTable(
                name: "FlashcardSessions");
        }
    }
}
