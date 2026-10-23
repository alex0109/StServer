using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionPositionAndResultRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing options get a stable order
            migrationBuilder.Sql("""
                UPDATE "Options" AS o
                SET "Position" = ordered.position
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "QuestionId" ORDER BY "Id") - 1 AS position
                    FROM "Options"
                ) AS ordered
                WHERE o."Id" = ordered."Id";
                """);

            // Same mapping as ConfidenceReviewRatingResolver, so old answers are usable by FSRS
            migrationBuilder.Sql("""
                UPDATE "Results"
                SET "Rating" = CASE
                    WHEN NOT "IsCorrect" THEN 1
                    WHEN "ConfidenceLevel" = 0 THEN 2
                    WHEN "ConfidenceLevel" = 2 THEN 4
                    ELSE 3
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Options");
        }
    }
}
