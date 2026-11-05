using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class DeadlineTimerAndHesitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Deadline",
                table: "Materials",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimerMinutes",
                table: "Assessments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "TimerMinutes",
                table: "Assessments");
        }
    }
}
