using Microsoft.AspNetCore.Mvc;
using RedactEngine.Application.Common;

namespace RedactEngine.ApiService.Controllers;

[ApiController]
[Route("api/prompts")]
public sealed class PromptTranslationController(ILlmPromptTranslator translator) : ControllerBase
{
    /// <summary>
    /// Translates a natural-language redaction instruction into a
    /// Grounding DINO-compatible detection prompt. Stateless preview —
    /// does not create a job.
    /// </summary>
    [HttpPost("translate")]
    public async Task<ActionResult<TranslatePromptResponse>> TranslateAsync(
        [FromBody] TranslatePromptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest("A prompt is required.");

        var result = await translator.TranslateAsync(request.Prompt, cancellationToken);
        return Ok(new TranslatePromptResponse(result.DetectionPrompt, result.IsFallback, result.Warning));
    }
}

public sealed record TranslatePromptRequest(string Prompt);

public sealed record TranslatePromptResponse(string DetectionPrompt, bool IsFallback, string? Warning);
