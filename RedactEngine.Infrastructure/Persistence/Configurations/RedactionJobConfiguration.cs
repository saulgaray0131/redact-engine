using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedactEngine.Domain.Entities;
using RedactEngine.Domain.ValueObjects;
using RedactEngine.Infrastructure.Persistence.Converters;

namespace RedactEngine.Infrastructure.Persistence.Configurations;

public class RedactionJobConfiguration : IEntityTypeConfiguration<RedactionJob>
{
    public void Configure(EntityTypeBuilder<RedactionJob> builder)
    {
        builder.ToTable("redaction_jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Prompt)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(j => j.RedactionStyle)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(j => j.ConfidenceThreshold)
            .IsRequired();

        builder.Property(j => j.OriginalVideoUrl)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(j => j.OriginalFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(j => j.RedactedVideoUrl)
            .HasMaxLength(2000);

        builder.Property(j => j.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(j => j.DetectionPreviewUrl)
            .HasMaxLength(2000);

        builder.Property(j => j.VideoMetadata)
            .HasConversion(new NullableJsonValueConverter<VideoMetadata>())
            .HasColumnType("jsonb");
        builder.Property(j => j.VideoMetadata)
            .Metadata.SetValueComparer(JsonValueComparer.CreateNullable<VideoMetadata>());

        builder.Property(j => j.DetectionSummary)
            .HasConversion(new NullableJsonValueConverter<DetectionSummary>())
            .HasColumnType("jsonb");
        builder.Property(j => j.DetectionSummary)
            .Metadata.SetValueComparer(JsonValueComparer.CreateNullable<DetectionSummary>());

        builder.Property(j => j.ProcessingMetrics)
            .HasConversion(new NullableJsonValueConverter<ProcessingMetrics>())
            .HasColumnType("jsonb");
        builder.Property(j => j.ProcessingMetrics)
            .Metadata.SetValueComparer(JsonValueComparer.CreateNullable<ProcessingMetrics>());

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_redaction_jobs_status");

        builder.HasIndex(j => j.CreatedAt)
            .HasDatabaseName("ix_redaction_jobs_created_at");
    }
}
