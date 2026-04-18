namespace RedactEngine.Application.Common;

public interface ILlmPromptTranslator
{
    Task<PromptTranslationResult> TranslateAsync(string userPrompt, CancellationToken cancellationToken = default);
}

public sealed record PromptTranslationResult(string DetectionPrompt, bool IsFallback, string? Warning);
