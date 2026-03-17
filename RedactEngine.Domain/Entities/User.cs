using RedactEngine.Domain.Common;
using RedactEngine.Domain.Events;

namespace RedactEngine.Domain.Entities;

public class User : Entity
{
    public string Auth0UserId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public UserRole Role { get; private set; }

    private User() { }

    public User(Guid id, string auth0UserId, string email, string? displayName = null, UserRole role = UserRole.User)
    {
        Id = id;
        Auth0UserId = string.IsNullOrWhiteSpace(auth0UserId)
            ? throw new ArgumentException("Auth0 user id is required.", nameof(auth0UserId))
            : auth0UserId;
        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        Role = role;

        AddDomainEvent(new UserCreatedEvent(Id, Email, Role));
    }

    public void UpdateProfile(string email, string? displayName)
    {
        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        UpdateTimestamp();
    }

    public void SetRole(UserRole role)
    {
        Role = role;
        UpdateTimestamp();
    }
}
