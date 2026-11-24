using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class ProfileAutoTranslate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoTranslate",
                table: "UserProfiles",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoTranslate",
                table: "UserProfiles");
        }
    }
}
