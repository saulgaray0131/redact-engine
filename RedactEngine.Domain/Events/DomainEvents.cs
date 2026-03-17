using RedactEngine.Domain.Common;
using RedactEngine.Domain.Entities;

namespace RedactEngine.Domain.Events;

public sealed class UserCreatedEvent : DomainEvent
{
    public UserCreatedEvent(Guid userId, string email, UserRole role)
    {
        UserId = userId;
        Email = email;
        Role = role;
    }

    public Guid UserId { get; }
    public string Email { get; }
    public UserRole Role { get; }
}