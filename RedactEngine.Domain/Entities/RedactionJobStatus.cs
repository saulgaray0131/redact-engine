namespace RedactEngine.Domain.Entities;

public enum RedactionJobStatus
{
    Pending = 0,
    Detecting = 1,
    AwaitingReview = 2,
    Redacting = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}
