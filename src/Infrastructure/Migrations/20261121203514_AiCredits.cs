using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AiCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Credits",
                table: "AiUsages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "AiUsages",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFallback",
                table: "AiUsages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "AiUsages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Units",
                table: "AiUsages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "VideoSeconds",
                table: "AiUsages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    UsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Feature = table.Column<int>(type: "integer", nullable: true),
                    PackId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StripeReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditWallets",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false),
                    AutoUseForStudy = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditWallets", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsages_UserId_Source_Feature",
                table: "AiUsages",
                columns: new[] { "UserId", "Source", "Feature" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_IdempotencyKey",
                table: "CreditTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_StripeReference",
                table: "CreditTransactions",
                column: "StripeReference");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_UserId_CreatedAt",
                table: "CreditTransactions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "CreditWallets");

            migrationBuilder.DropIndex(
                name: "IX_AiUsages_UserId_Source_Feature",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "Credits",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "IsFallback",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "Units",
                table: "AiUsages");

            migrationBuilder.DropColumn(
                name: "VideoSeconds",
                table: "AiUsages");
        }
    }
}
