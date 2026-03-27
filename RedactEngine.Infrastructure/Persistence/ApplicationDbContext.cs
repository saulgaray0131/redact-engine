using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RedactEngine.Application.Common;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Common;
using RedactEngine.Domain.Entities;
using RedactEngine.Domain.Repositories;
using RedactEngine.Infrastructure.Persistence.Configurations;

namespace RedactEngine.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IUnitOfWork, IApplicationDbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RedactionJob> RedactionJobs => Set<RedactionJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RedactionJobConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events before saving
        var entitiesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(x => x.DomainEvents)
            .ToList();

        // Convert domain events to outbox messages
        var outboxMessages = domainEvents
            .Select(domainEvent => OutboxMessage.Create(
                domainEvent,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType())))
            .ToList();

        // Add outbox messages to context
        OutboxMessages.AddRange(outboxMessages);

        // Clear domain events from entities
        entitiesWithEvents.ForEach(entity => entity.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_domainEventDispatcher is null || domainEvents.Count == 0)
        {
            return result;
        }

        foreach (var domainEvent in domainEvents)
        {
            await _domainEventDispatcher.DispatchEventAsync(domainEvent, cancellationToken);
        }

        return result;
    }
}