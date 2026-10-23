using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "Materials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportedFromMaterialId",
                table: "Materials",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialShares_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_UserId_ImportedFromMaterialId",
                table: "Materials",
                columns: new[] { "UserId", "ImportedFromMaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialShares_MaterialId_UserId",
                table: "MaterialShares",
                columns: new[] { "MaterialId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialShares_Token",
                table: "MaterialShares",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialShares");

            migrationBuilder.DropIndex(
                name: "IX_Materials_UserId_ImportedFromMaterialId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ImportedFromMaterialId",
                table: "Materials");
        }
    }
}
