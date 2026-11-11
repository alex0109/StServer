using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class SpeechUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpeechUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PodcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Characters = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeechUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpeechUsages_CreatedAt",
                table: "SpeechUsages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SpeechUsages_UserId",
                table: "SpeechUsages",
                column: "UserId");

            // Podcasts recorded before the log existed: their spoken characters are in the saved
            // script (Status 1 = Ready). Failed ones can't be counted, the voiced part is unknown
            migrationBuilder.Sql("""
                INSERT INTO "SpeechUsages" ("Id", "UserId", "PodcastId", "Language", "Characters", "Succeeded", "CreatedAt")
                SELECT gen_random_uuid(), p."UserId", p."Id", p."Language",
                       COALESCE((SELECT SUM(char_length(line ->> 'text'))
                                 FROM jsonb_array_elements(p."Script" -> 'chapters') AS chapter,
                                      jsonb_array_elements(chapter -> 'lines') AS line), 0),
                       TRUE, COALESCE(p."CompletedAt", p."UpdatedAt")
                FROM "MaterialPodcasts" AS p
                WHERE p."Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpeechUsages");
        }
    }
}
