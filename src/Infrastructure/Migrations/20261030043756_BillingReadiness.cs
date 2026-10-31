using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class BillingReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "UserSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodStart",
                table: "UserSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantReason",
                table: "UserSubscriptions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByAdminId",
                table: "UserSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEventAt",
                table: "UserSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "UserSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCustomerId",
                table: "UserSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubscriptionId",
                table: "UserSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "UserSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "UserSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Provider_ProviderCustomerId",
                table: "UserSubscriptions",
                columns: new[] { "Provider", "ProviderCustomerId" },
                unique: true,
                filter: "\"ProviderCustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Provider_ProviderSubscriptionId",
                table: "UserSubscriptions",
                columns: new[] { "Provider", "ProviderSubscriptionId" },
                unique: true,
                filter: "\"ProviderSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_Provider_ProviderEventId",
                table: "SubscriptionEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_ReceivedAt",
                table: "SubscriptionEvents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_UserId",
                table: "SubscriptionEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionEvents");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_Provider_ProviderCustomerId",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_Provider_ProviderSubscriptionId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStart",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GrantReason",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GrantedByAdminId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastEventAt",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderCustomerId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderSubscriptionId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "UserSubscriptions");
        }
    }
}
