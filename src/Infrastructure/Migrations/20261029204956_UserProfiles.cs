using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class UserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiExplanationLanguage",
                table: "Results",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InterfaceLanguage = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ExplanationLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    NativeLanguage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StudyLanguage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DailyGoal = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                });

            // Every user with reminder settings gets a profile with the same time zone and language,
            // and the nickname of their latest library request. The old columns stay for now
            migrationBuilder.Sql("""
                INSERT INTO "UserProfiles" ("UserId", "TimeZone", "InterfaceLanguage", "Nickname", "CreatedAt", "UpdatedAt")
                SELECT s."UserId",
                       s."TimeZone",
                       CASE WHEN s."Language" IN ('en-GB', 'nb', 'uk') THEN s."Language" ELSE 'en-GB' END,
                       (SELECT r."AuthorNickname"
                        FROM "LibraryRequests" r
                        WHERE r."UserId" = s."UserId"
                        ORDER BY r."CreatedAt" DESC
                        LIMIT 1),
                       s."CreatedAt",
                       now()
                FROM "UserNotificationSettings" s
                ON CONFLICT ("UserId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AiExplanationLanguage",
                table: "Results");
        }
    }
}
