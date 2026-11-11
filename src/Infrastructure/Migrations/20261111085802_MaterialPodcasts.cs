using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class MaterialPodcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialPodcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Script = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ChapterDurations = table.Column<double[]>(type: "double precision[]", nullable: false),
                    AudioPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AudioSize = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialPodcasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialPodcasts_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPodcasts_MaterialId",
                table: "MaterialPodcasts",
                column: "MaterialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPodcasts_UserId",
                table: "MaterialPodcasts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialPodcasts");
        }
    }
}
