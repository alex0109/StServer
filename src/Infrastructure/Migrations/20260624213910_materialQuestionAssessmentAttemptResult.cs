using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StServer.Migrations
{
    /// <inheritdoc />
    public partial class materialQuestionAssessmentAttemptResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Results_Assessments_AssessmentId",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_Results_AssessmentId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CorrectAnswers",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "TotalQuestions",
                table: "Assessments");

            migrationBuilder.RenameColumn(
                name: "AssessmentId",
                table: "Results",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Materials",
                newName: "Content");

            migrationBuilder.AddColumn<int>(
                name: "AnswerChangedCount",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptId",
                table: "Results",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceLevel",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TimeSpent",
                table: "Results",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AlterColumn<string>(
                name: "Answer",
                table: "Questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectOptionId",
                table: "Questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionDifficulty",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuestionType",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Questions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ALTER COLUMN ""Type"" TYPE integer
                USING (CASE ""Type""
                    WHEN 'article' THEN 0
                    WHEN 'video' THEN 1
                    WHEN 'summary' THEN 2
                    WHEN 'practice' THEN 3
                    WHEN 'test' THEN 4
                END);
            ");

            //migrationBuilder.AlterColumn<int>(
            //    name: "Type",
            //    table: "Materials",
            //    type: "integer",
            //    nullable: false,
            //    oldClrType: typeof(string),
            //    oldType: "text");
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ALTER COLUMN ""Status"" TYPE integer
                USING (CASE ""Status""
                    WHEN 'tolearn' THEN 0
                    WHEN 'inprocess' THEN 1
                    WHEN 'finished' THEN 2
                END);
            ");

            //migrationBuilder.AlterColumn<int>(
            //    name: "Status",
            //    table: "Materials",
            //    type: "integer",
            //    nullable: false,
            //    oldClrType: typeof(string),
            //    oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Materials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Materials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Materials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Materials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Assessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptStatus = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attempts_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Options_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialTags",
                columns: table => new
                {
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialTags", x => new { x.MaterialId, x.TagId });
                    table.ForeignKey(
                        name: "FK_MaterialTags_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Results_AttemptId",
                table: "Results",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_AssessmentId",
                table: "Attempts",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTags_TagId",
                table: "MaterialTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Options_QuestionId",
                table: "Options",
                column: "QuestionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Results_Attempts_AttemptId",
                table: "Results",
                column: "AttemptId",
                principalTable: "Attempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Results_Attempts_AttemptId",
                table: "Results");

            migrationBuilder.DropTable(
                name: "Attempts");

            migrationBuilder.DropTable(
                name: "MaterialTags");

            migrationBuilder.DropTable(
                name: "Options");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Results_AttemptId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "AnswerChangedCount",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "ConfidenceLevel",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "TimeSpent",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "CorrectOptionId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "QuestionDifficulty",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Assessments");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Results",
                newName: "AssessmentId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Materials",
                newName: "Description");

            migrationBuilder.AlterColumn<string>(
                name: "Answer",
                table: "Questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Materials",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Materials",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string[]>(
                name: "Tags",
                table: "Materials",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectAnswers",
                table: "Assessments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "Assessments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Assessments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "Assessments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Results_AssessmentId",
                table: "Results",
                column: "AssessmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Results_Assessments_AssessmentId",
                table: "Results",
                column: "AssessmentId",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
