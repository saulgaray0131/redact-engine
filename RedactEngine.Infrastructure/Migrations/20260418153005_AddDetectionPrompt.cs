using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedactEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectionPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetectionPrompt",
                table: "redaction_jobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionPrompt",
                table: "redaction_jobs");
        }
    }
}
