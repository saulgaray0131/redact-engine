using Microsoft.Extensions.Logging;
using RedactEngine.Application.Common;

namespace RedactEngine.Infrastructure.Services;

public sealed class MockPromptTranslator : ILlmPromptTranslator
{
    private readonly ILogger<MockPromptTranslator> _logger;

    public MockPromptTranslator(ILogger<MockPromptTranslator> logger)
    {
        _logger = logger;
    }

    public Task<PromptTranslationResult> TranslateAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock translator passing prompt through unchanged: {Prompt}", userPrompt);
        return Task.FromResult(new PromptTranslationResult(userPrompt.Trim(), IsFallback: false, Warning: null));
    }
}
