using RedactEngine.Domain.Common;

namespace RedactEngine.Application.Common;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }

    public static OutboxMessage Create(IDomainEvent domainEvent, string serializedData)
    {
        return new OutboxMessage
        {
            Type = domainEvent.GetType().Name,
            Data = serializedData
        };
    }

    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
        RetryCount++;
    }
}
