using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedactEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetectionPreviewUrl",
                table: "redaction_jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionSummary",
                table: "redaction_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingMetrics",
                table: "redaction_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoMetadata",
                table: "redaction_jobs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionPreviewUrl",
                table: "redaction_jobs");

            migrationBuilder.DropColumn(
                name: "DetectionSummary",
                table: "redaction_jobs");

            migrationBuilder.DropColumn(
                name: "ProcessingMetrics",
                table: "redaction_jobs");

            migrationBuilder.DropColumn(
                name: "VideoMetadata",
                table: "redaction_jobs");
        }
    }
}
