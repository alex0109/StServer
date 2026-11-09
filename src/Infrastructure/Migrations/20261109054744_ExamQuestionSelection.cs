using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class ExamQuestionSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "QuestionIds",
                table: "Attempts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<int[]>(
                name: "Difficulties",
                table: "Assessments",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<int>(
                name: "QuestionCount",
                table: "Assessments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionIds",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "Difficulties",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "QuestionCount",
                table: "Assessments");
        }
    }
}
