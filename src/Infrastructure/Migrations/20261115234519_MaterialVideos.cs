using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class MaterialVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VideoSeconds",
                table: "Questions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Transcript = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    IsClipped = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialVideos_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialVideos_MaterialId",
                table: "MaterialVideos",
                column: "MaterialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialVideos_UserId",
                table: "MaterialVideos",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialVideos");

            migrationBuilder.DropColumn(
                name: "VideoSeconds",
                table: "Questions");
        }
    }
}
