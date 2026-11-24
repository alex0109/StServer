using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class EssayExamFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "EssayFormat",
                table: "Notes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes",
                sql: "\"Type\" = 4 OR (\"EssayTask\" IS NULL AND \"EssayReview\" IS NULL AND \"EssayFormat\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EssayFormat",
                table: "Notes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Note_EssayFields",
                table: "Notes",
                sql: "\"Type\" = 4 OR (\"EssayTask\" IS NULL AND \"EssayReview\" IS NULL)");
        }
    }
}
