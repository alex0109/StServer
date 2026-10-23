using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class NotesId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NoteId",
                table: "Questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_NoteId",
                table: "Questions",
                column: "NoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Notes_NoteId",
                table: "Questions",
                column: "NoteId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Notes_NoteId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_NoteId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "NoteId",
                table: "Questions");
        }
    }
}
