using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class NotesTypesEdit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Notes\" ALTER COLUMN \"TextContent\" TYPE jsonb USING \"TextContent\"::jsonb;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Notes\" ALTER COLUMN \"DrawingContent\" TYPE jsonb USING \"DrawingContent\"::jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Notes\" ALTER COLUMN \"TextContent\" TYPE text USING \"TextContent\"::text;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Notes\" ALTER COLUMN \"DrawingContent\" TYPE text USING \"DrawingContent\"::text;");
        }
    }
}
