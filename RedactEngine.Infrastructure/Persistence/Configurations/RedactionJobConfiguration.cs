using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedactEngine.Domain.Entities;

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

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_redaction_jobs_status");

        builder.HasIndex(j => j.CreatedAt)
            .HasDatabaseName("ix_redaction_jobs_created_at");
    }
}
