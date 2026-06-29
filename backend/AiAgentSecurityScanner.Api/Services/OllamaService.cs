using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public class OllamaLLMProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    IOptions<SemanticCacheOptions> cacheOptions,
    ILogger<OllamaLLMProvider> logger) : ILLMProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string ProviderName => "Ollama";
    public string ModelName => options.Value.DefaultModel;

    public async Task<LLMChatResult> SendChatAsync(string prompt, string? requestedModel, CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var primaryModel = string.IsNullOrWhiteSpace(requestedModel) ? configured.DefaultModel : requestedModel.Trim();

        try
        {
            return await SendAsync(primaryModel, prompt, cancellationToken);
        }
        catch when (!string.Equals(primaryModel, configured.FallbackModel, StringComparison.OrdinalIgnoreCase))
        {
            return await SendAsync(configured.FallbackModel, prompt, cancellationToken);
        }
    }

    public async Task<float[]?> GetEmbeddingAsync(string normalizedPrompt, CancellationToken cancellationToken)
    {
        var model = cacheOptions.Value.EmbeddingModel;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var baseUrl = options.Value.BaseUrl.TrimEnd('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/embeddings");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new EmbeddingRequest(model, normalizedPrompt), JsonOptions),
                Encoding.UTF8,
                "application/json");

            logger.LogInformation("Embedding request starting. Provider={Provider} Model={Model} PromptLength={PromptLength}", ProviderName, model, normalizedPrompt.Length);
            using var response = await httpClientFactory.CreateClient("Ollama").SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Embedding request failed. Provider={Provider} Model={Model} StatusCode={StatusCode} DurationMs={DurationMs} Payload={Payload}",
                    ProviderName, model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, payload);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<EmbeddingResponse>(payload, JsonOptions);
            logger.LogInformation("Embedding request completed. Provider={Provider} Model={Model} DurationMs={DurationMs} Dimensions={Dimensions}",
                ProviderName, model, stopwatch.ElapsedMilliseconds, parsed?.Embedding?.Length ?? 0);
            return parsed?.Embedding;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Embedding request failed. Provider={Provider} Model={Model} DurationMs={DurationMs}", ProviderName, model, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    private async Task<LLMChatResult> SendAsync(string model, string prompt, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/chat");
        var messages = string.IsNullOrWhiteSpace(options.Value.SystemPrompt)
            ? [new OllamaMessage("user", prompt)]
            : new[] { new OllamaMessage("system", options.Value.SystemPrompt), new OllamaMessage("user", prompt) };
        var body = new OllamaChatRequest(
            model,
            messages,
            false,
            options.Value.KeepAlive,
            new OllamaRequestOptions(options.Value.NumPredict));
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        logger.LogInformation("LLM request starting. Provider={Provider} Model={Model} PromptLength={PromptLength} NumPredict={NumPredict} Stream={Stream}", ProviderName, model, prompt.Length, options.Value.NumPredict, false);
        using var response = await httpClientFactory.CreateClient("Ollama").SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}: {payload}");

        var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(payload, JsonOptions);
        var content = ollamaResponse?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            content = payload;
        logger.LogInformation(
            "LLM request completed. Provider={Provider} Model={Model} StatusCode={StatusCode} DurationMs={DurationMs} PayloadLength={PayloadLength} ResponseLength={ResponseLength} PromptTokens={PromptTokens} ResponseTokens={ResponseTokens} DoneReason={DoneReason}",
            ProviderName,
            model,
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            payload.Length,
            content.Length,
            ollamaResponse?.PromptEvalCount,
            ollamaResponse?.EvalCount,
            ollamaResponse?.DoneReason);

        int? totalTokens = ollamaResponse?.PromptEvalCount is null && ollamaResponse?.EvalCount is null
            ? null
            : (ollamaResponse?.PromptEvalCount ?? 0) + (ollamaResponse?.EvalCount ?? 0);

        return new LLMChatResult(
            ProviderName,
            model,
            content,
            response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            ollamaResponse?.PromptEvalCount,
            ollamaResponse?.EvalCount,
            totalTokens,
            1,
            ollamaResponse?.DoneReason);
    }

    private record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("keep_alive")] string KeepAlive,
        [property: JsonPropertyName("options")] OllamaRequestOptions Options);

    private record OllamaRequestOptions(
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("done")] bool Done,
        [property: JsonPropertyName("done_reason")] string? DoneReason,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount);

    private record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
