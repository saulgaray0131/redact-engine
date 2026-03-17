using RedactEngine.Domain.Common;

namespace RedactEngine.Application.Common.Interfaces;

/// <summary>
/// Interface for dispatching domain events from entities.
/// Domain events are dispatched via MediatR after being collected from entities.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a single domain event.
    /// The event is published via MediatR.
    /// </summary>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches all pending domain events from a single entity.
    /// Events are published via MediatR and then cleared from the entity.
    /// </summary>
    /// <param name="entity">The entity containing domain events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchEventsAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches all pending domain events from multiple entities.
    /// Events are published via MediatR and then cleared from all entities.
    /// </summary>
    /// <param name="entities">The entities containing domain events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchEventsAsync(IEnumerable<Entity> entities, CancellationToken cancellationToken = default);
}
