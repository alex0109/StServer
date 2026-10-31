using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AiIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Material type "summary" (2) is removed, a summary is now a note
            migrationBuilder.Sql("UPDATE \"Materials\" SET \"Type\" = 0 WHERE \"Type\" = 2;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "AiExplanation",
                table: "Results",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AiUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_SingleSummary",
                table: "Notes",
                column: "MaterialId",
                unique: true,
                filter: "\"Type\" = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes",
                sql: "(\"Type\" = 0 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 1 AND \"TextContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 3 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_Status_CreatedAt",
                table: "AiUsages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_UserId_Feature_CreatedAt",
                table: "AiUsages",
                columns: new[] { "UserId", "Feature", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsages");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Notes_SingleSummary",
                table: "Notes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes");

            // The old constraint has no summary notes, they stay as text notes
            migrationBuilder.Sql("UPDATE \"Notes\" SET \"Type\" = 0 WHERE \"Type\" = 3;");

            migrationBuilder.DropColumn(
                name: "AiExplanation",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Results");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes",
                sql: "(\"Type\" = 0 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 1 AND \"TextContent\" IS NULL AND \"FileId\" IS NULL)");
        }
    }
}
