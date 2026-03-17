using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedactEngine.Domain.Entities;

namespace RedactEngine.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Auth0UserId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200);

        builder.Property(user => user.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(user => user.Auth0UserId)
            .IsUnique();

        builder.HasIndex(user => user.Email)
            .IsUnique();
    }
}
