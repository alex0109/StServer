using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class LibraryAndFeedbackLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportedFromLibraryItemId",
                table: "Materials",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FeedbackSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AuthorNickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    NotesCount = table.Column<int>(type: "integer", nullable: false),
                    QuestionsCount = table.Column<int>(type: "integer", nullable: false),
                    DictionariesCount = table.Column<int>(type: "integer", nullable: false),
                    WordsCount = table.Column<int>(type: "integer", nullable: false),
                    ImportsCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    MaterialType = table.Column<int>(type: "integer", nullable: false),
                    AuthorNickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_UserId_ImportedFromLibraryItemId",
                table: "Materials",
                columns: new[] { "UserId", "ImportedFromLibraryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSubmissions_CreatedAt",
                table: "FeedbackSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSubmissions_SenderKey_CreatedAt",
                table: "FeedbackSubmissions",
                columns: new[] { "SenderKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItems_AuthorUserId",
                table: "LibraryItems",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItems_Status_PublishedAt",
                table: "LibraryItems",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_LibraryItemId_Status",
                table: "LibraryRequests",
                columns: new[] { "LibraryItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_MaterialId_Status",
                table: "LibraryRequests",
                columns: new[] { "MaterialId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_Status_CreatedAt",
                table: "LibraryRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_UserId_Status",
                table: "LibraryRequests",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackSubmissions");

            migrationBuilder.DropTable(
                name: "LibraryItems");

            migrationBuilder.DropTable(
                name: "LibraryRequests");

            migrationBuilder.DropIndex(
                name: "IX_Materials_UserId_ImportedFromLibraryItemId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ImportedFromLibraryItemId",
                table: "Materials");
        }
    }
}
