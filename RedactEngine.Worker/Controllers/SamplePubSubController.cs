using Dapr;
using Microsoft.AspNetCore.Mvc;
using RedactEngine.Shared.PubSub;

namespace RedactEngine.Worker.Controllers;

[ApiController]
public sealed class SamplePubSubController(ILogger<SamplePubSubController> logger) : ControllerBase
{
    [Topic(SamplePubSub.ComponentName, SamplePubSub.TopicName)]
    [HttpPost("samples/pubsub")]
    public IActionResult Receive([FromBody] SamplePubSubMessage message)
    {
        logger.LogInformation(
            "Received sample pub/sub event {EventId} from {Source} at {CreatedAtUtc} with message: {Message}",
            message.EventId,
            message.Source,
            message.CreatedAtUtc,
            message.Message);

        return Ok();
    }
}