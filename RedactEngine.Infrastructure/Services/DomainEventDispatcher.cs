using Microsoft.Extensions.Logging;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Common;

namespace RedactEngine.Infrastructure.Services;

public class DomainEventDispatcher(ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{

    /// <inheritdoc />
    public async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        try
        {
            logger.LogInformation(
                "Dispatched domain event {EventType} with id {EventId}",
                domainEvent.GetType().Name,
                domainEvent.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error dispatching domain event: {EventType}, Id={EventId}",
                domainEvent.GetType().Name,
                domainEvent.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DispatchEventsAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await DispatchEventsAsync([entity], cancellationToken);
    }

    /// <inheritdoc />
    public async Task DispatchEventsAsync(IEnumerable<Entity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities.ToList();

        // Collect all domain events from entities
        var domainEvents = entityList
            .SelectMany(e => e.DomainEvents)
            .ToList();

        if (domainEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Dispatching {EventCount} domain events from {EntityCount} entities",
            domainEvents.Count,
            entityList.Count);

        // Dispatch each event using the single-event dispatcher
        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, cancellationToken);
        }

        // Clear events from all entities after successful dispatch
        foreach (var entity in entityList)
        {
            entity.ClearDomainEvents();
        }

        logger.LogDebug(
            "Successfully dispatched {EventCount} domain events",
            domainEvents.Count);
    }
}
