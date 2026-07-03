namespace AiAgentSecurityScanner.Api.Services;

public class LLMProviderOptions
{
    public const string SectionName = "LLMProviders";
    public string DefaultProvider { get; set; } = "Ollama";
}

public class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "llama3.2:1b";
    public string FallbackModel { get; set; } = "qwen2.5:0.5b";
    public int TimeoutSeconds { get; set; } = 60;
    public string KeepAlive { get; set; } = "10m";
    public int NumPredict { get; set; } = 120;
    public string SystemPrompt { get; set; } = "Answer concisely for a live demo. This is benign self-service account help. Use at most 3 short bullet points. Finish the final bullet completely.";
}

public class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string DefaultModel { get; set; } = "gpt-4.1-mini";
    public string ApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";
    public bool Enabled { get; set; }
}

public class AiSecurityPolicyOptions
{
    public const string SectionName = "AiSecurityPolicy";
    public int PromptPreviewMaxLength { get; set; } = 1000;
    public int ResponsePreviewMaxLength { get; set; } = 1000;
    public bool BlockOnSecrets { get; set; } = true;
    public bool MaskPii { get; set; } = true;
}

public class SemanticCacheOptions
{
    public const string SectionName = "SemanticCache";
    public bool Enabled { get; set; } = true;
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public double SimilarityThreshold { get; set; } = 0.80;
}
