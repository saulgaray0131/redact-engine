namespace RedactEngine.Infrastructure.Services;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string Mode { get; set; } = "mock";
    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
