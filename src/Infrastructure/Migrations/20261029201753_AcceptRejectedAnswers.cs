using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AcceptRejectedAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OriginalIsCorrect",
                table: "Results",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalMethod",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginalScore",
                table: "Results",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverriddenAt",
                table: "Results",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalIsCorrect",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "OriginalMethod",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "OriginalScore",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "OverriddenAt",
                table: "Results");
        }
    }
}
