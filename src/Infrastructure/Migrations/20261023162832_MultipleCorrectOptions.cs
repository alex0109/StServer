using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class MultipleCorrectOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Options",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "UserAnswerOptionIds",
                table: "Results",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            // Move data to the new columns before the old ones are dropped
            migrationBuilder.Sql("""
                UPDATE "Options" AS o
                SET "IsCorrect" = TRUE
                FROM "Questions" AS q
                WHERE o."QuestionId" = q."Id" AND q."CorrectOptionId" = o."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "Results"
                SET "UserAnswerOptionIds" = ARRAY["UserAnswerOptionId"]
                WHERE "UserAnswerOptionId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "UserAnswerOptionId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "CorrectOptionId",
                table: "Questions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserAnswerOptionId",
                table: "Results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectOptionId",
                table: "Questions",
                type: "uuid",
                nullable: true);

            // Single value columns can keep only the first correct option and the first chosen one
            migrationBuilder.Sql("""
                UPDATE "Questions" AS q
                SET "CorrectOptionId" = (
                    SELECT o."Id" FROM "Options" AS o
                    WHERE o."QuestionId" = q."Id" AND o."IsCorrect"
                    ORDER BY o."Position"
                    LIMIT 1);
                """);

            migrationBuilder.Sql("""
                UPDATE "Results"
                SET "UserAnswerOptionId" = "UserAnswerOptionIds"[1]
                WHERE cardinality("UserAnswerOptionIds") > 0;
                """);

            migrationBuilder.DropColumn(
                name: "UserAnswerOptionIds",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Options");
        }
    }
}
