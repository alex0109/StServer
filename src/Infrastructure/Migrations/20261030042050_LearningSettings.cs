using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class LearningSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyReminderHour",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DesiredRetention",
                table: "UserProfiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxReviewsPerDay",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewCardsPerDay",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StreakFreezesPerMonth",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StreakReminderHour",
                table: "UserProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyReminderHour",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DesiredRetention",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "MaxReviewsPerDay",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "NewCardsPerDay",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "StreakFreezesPerMonth",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "StreakReminderHour",
                table: "UserProfiles");
        }
    }
}
