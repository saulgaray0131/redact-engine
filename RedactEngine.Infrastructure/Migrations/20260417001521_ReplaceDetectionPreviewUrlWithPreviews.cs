using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedactEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDetectionPreviewUrlWithPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionPreviewUrl",
                table: "redaction_jobs");

            migrationBuilder.AddColumn<string>(
                name: "DetectionPreviews",
                table: "redaction_jobs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionPreviews",
                table: "redaction_jobs");

            migrationBuilder.AddColumn<string>(
                name: "DetectionPreviewUrl",
                table: "redaction_jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
