using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public interface IOllamaEmbeddingService
{
    Task<float[]?> GenerateEmbeddingAsync(string normalizedPrompt, CancellationToken cancellationToken);
}

public class OllamaEmbeddingService(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<SemanticCacheOptions> cacheOptions,
    ILogger<OllamaEmbeddingService> logger) : IOllamaEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<float[]?> GenerateEmbeddingAsync(string normalizedPrompt, CancellationToken cancellationToken)
    {
        var model = cacheOptions.Value.EmbeddingModel;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var baseUrl = ollamaOptions.Value.BaseUrl.TrimEnd('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/embeddings");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new EmbeddingRequest(model, normalizedPrompt), JsonOptions),
                Encoding.UTF8,
                "application/json");

            logger.LogInformation("Embedding request starting. Model={Model} PromptLength={PromptLength}", model, normalizedPrompt.Length);
            using var response = await httpClientFactory.CreateClient("Ollama").SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Embedding request failed. Model={Model} StatusCode={StatusCode} DurationMs={DurationMs} Payload={Payload}",
                    model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, payload);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<EmbeddingResponse>(payload, JsonOptions);
            logger.LogInformation("Embedding request completed. Model={Model} DurationMs={DurationMs} Dimensions={Dimensions}",
                model, stopwatch.ElapsedMilliseconds, parsed?.Embedding?.Length ?? 0);
            return parsed?.Embedding;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Embedding request failed. Model={Model} DurationMs={DurationMs}", model, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    private record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
