using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public record OllamaChatResult(
    string Model,
    string Content,
    HttpStatusCode StatusCode,
    long DurationMs,
    int? PromptTokens,
    int? ResponseTokens,
    int? TotalTokens,
    int StreamedChunksReceived,
    string? DoneReason);

public interface IOllamaService
{
    Task<OllamaChatResult> ChatAsync(string prompt, string? requestedModel, CancellationToken cancellationToken);
}

public class OllamaService(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    ILogger<OllamaService> logger) : IOllamaService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OllamaChatResult> ChatAsync(string prompt, string? requestedModel, CancellationToken cancellationToken)
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

    private async Task<OllamaChatResult> SendAsync(string model, string prompt, CancellationToken cancellationToken)
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

        logger.LogInformation("Ollama request starting. Model={Model} PromptLength={PromptLength} NumPredict={NumPredict} Stream={Stream}", model, prompt.Length, options.Value.NumPredict, false);
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
            "Ollama request completed. Model={Model} StatusCode={StatusCode} DurationMs={DurationMs} PayloadLength={PayloadLength} ResponseLength={ResponseLength} PromptTokens={PromptTokens} ResponseTokens={ResponseTokens} DoneReason={DoneReason}",
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

        return new OllamaChatResult(
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
}
