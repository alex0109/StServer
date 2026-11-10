using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class EssayNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "EssayReview",
                table: "Notes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EssayTask",
                table: "Notes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes",
                sql: "\"Type\" = 4 OR (\"EssayTask\" IS NULL AND \"EssayReview\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes",
                sql: "(\"Type\" = 0 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 1 AND \"TextContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 3 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 4 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EssayReview",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EssayTask",
                table: "Notes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_TypeConsistency",
                table: "Notes",
                sql: "(\"Type\" = 0 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 1 AND \"TextContent\" IS NULL AND \"FileId\" IS NULL) OR\n(\"Type\" = 3 AND \"DrawingContent\" IS NULL AND \"FileId\" IS NULL)");
        }
    }
}
