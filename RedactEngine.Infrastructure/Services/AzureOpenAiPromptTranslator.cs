using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedactEngine.Application.Common;

namespace RedactEngine.Infrastructure.Services;

/// <summary>
/// Calls Azure OpenAI Chat Completions to convert natural-language redaction
/// instructions into Grounding DINO-compatible prompts
/// (period-separated short noun phrases).
/// </summary>
public sealed class AzureOpenAiPromptTranslator : ILlmPromptTranslator
{
    private const string ApiVersion = "2024-10-21";

    private static readonly string SystemPrompt = """
        You convert natural-language video redaction requests into prompts for the
        Grounding DINO open-vocabulary object detector. Grounding DINO expects a
        short list of concrete visual object categories.

        Respond with STRICT JSON in this exact shape:
        {"targets": ["<phrase>", "<phrase>", ...]}

        Rules for each phrase:
        - 1 to 3 words
        - singular, lowercase, no articles ("a", "the")
        - a concrete visible object or visible attribute (not an action, not a verb)
        - only include things the user wants redacted

        Examples:
        Input: "blur out anyone walking past and any visible screens"
        Output: {"targets": ["person", "laptop screen", "monitor"]}

        Input: "hide license plates and faces"
        Output: {"targets": ["license plate", "face"]}

        Input: "redact logos on shirts and any tattoos"
        Output: {"targets": ["logo", "tattoo"]}
        """;

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<LlmOptions> _options;
    private readonly ILogger<AzureOpenAiPromptTranslator> _logger;

    public AzureOpenAiPromptTranslator(
        HttpClient httpClient,
        IOptionsMonitor<LlmOptions> options,
        ILogger<AzureOpenAiPromptTranslator> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<PromptTranslationResult> TranslateAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(opts.Endpoint) || string.IsNullOrWhiteSpace(opts.Deployment) || string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            _logger.LogWarning("Azure OpenAI not configured; falling back to user prompt unchanged");
            return Fallback(userPrompt, "Azure OpenAI not configured");
        }

        var url = $"{opts.Endpoint.TrimEnd('/')}/openai/deployments/{opts.Deployment}/chat/completions?api-version={ApiVersion}";

        var requestBody = new ChatCompletionRequest(
            Messages:
            [
                new ChatMessage("system", SystemPrompt),
                new ChatMessage("user", userPrompt)
            ],
            Temperature: 0.1,
            MaxTokens: 200,
            ResponseFormat: new ResponseFormat("json_object"));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", opts.ApiKey);
            request.Content = JsonContent.Create(requestBody, options: SerializerOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Azure OpenAI returned {Status}: {Body}", response.StatusCode, body);
                return Fallback(userPrompt, $"Azure OpenAI returned {(int)response.StatusCode}");
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Fallback(userPrompt, "Empty LLM response");
            }

            var targets = JsonSerializer.Deserialize<TargetsPayload>(content, SerializerOptions);
            if (targets?.Targets is null || targets.Targets.Count == 0)
            {
                return Fallback(userPrompt, "LLM returned no targets");
            }

            var detectionPrompt = string.Join(". ", targets.Targets.Select(t => t.Trim().ToLowerInvariant())) + ".";
            _logger.LogInformation("Translated '{User}' -> '{Detection}'", userPrompt, detectionPrompt);
            return new PromptTranslationResult(detectionPrompt, IsFallback: false, Warning: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Azure OpenAI translation failed; falling back to user prompt");
            return Fallback(userPrompt, $"Translation failed: {ex.GetType().Name}");
        }
    }

    private static PromptTranslationResult Fallback(string userPrompt, string reason) =>
        new(userPrompt.Trim(), IsFallback: true, Warning: reason);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record ChatCompletionRequest(
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ResponseFormat([property: JsonPropertyName("type")] string Type);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice> Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatMessage Message);

    private sealed record TargetsPayload(
        [property: JsonPropertyName("targets")] List<string> Targets);
}
