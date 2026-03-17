using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedactEngine.Application.Common;

namespace RedactEngine.Infrastructure.Persistence.Configurations;

public class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(om => om.Id);

        builder.Property(om => om.Type)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(om => om.Data)
            .IsRequired();

        builder.Property(om => om.Error)
            .HasMaxLength(2000);

        builder.HasIndex(om => om.CreatedAt)
            .HasDatabaseName("ix_outbox_messages_created_at");

        builder.HasIndex(om => om.ProcessedAt)
            .HasDatabaseName("ix_outbox_messages_processed_at");
    }
}