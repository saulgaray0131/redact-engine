using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using RedactEngine.Shared.PubSub;

namespace RedactEngine.ApiService.Controllers;

[ApiController]
[Route("samples/pubsub")]
public sealed class SamplePubSubController(DaprClient daprClient) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PublishSamplePubSubResponse>> PublishAsync(
        [FromBody] PublishSamplePubSubRequest? request,
        CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(request?.Message)
            ? "Hello from RedactEngine.ApiService"
            : request.Message.Trim();

        var sampleEvent = new SamplePubSubMessage(
            EventId: Guid.NewGuid(),
            Message: message,
            Source: string.IsNullOrWhiteSpace(request?.Source)
                ? "RedactEngine.ApiService"
                : request.Source.Trim(),
            CreatedAtUtc: DateTimeOffset.UtcNow);

        await daprClient.PublishEventAsync(
            SamplePubSub.ComponentName,
            SamplePubSub.TopicName,
            sampleEvent,
            cancellationToken);

        return Accepted(new PublishSamplePubSubResponse(
            SamplePubSub.ComponentName,
            SamplePubSub.TopicName,
            sampleEvent.EventId,
            sampleEvent.CreatedAtUtc));
    }
}

public sealed record PublishSamplePubSubRequest(
    string? Message,
    string? Source);

public sealed record PublishSamplePubSubResponse(
    string Component,
    string Topic,
    Guid EventId,
    DateTimeOffset PublishedAtUtc);