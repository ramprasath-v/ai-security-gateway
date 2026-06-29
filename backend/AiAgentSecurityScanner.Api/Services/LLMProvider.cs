using System.Net;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public record LLMChatResult(
    string Provider,
    string Model,
    string Content,
    HttpStatusCode StatusCode,
    long DurationMs,
    int? PromptTokens,
    int? ResponseTokens,
    int? TotalTokens,
    int StreamedChunksReceived,
    string? DoneReason);

public interface ILLMProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<LLMChatResult> SendChatAsync(string prompt, string? requestedModel, CancellationToken cancellationToken);
    Task<float[]?> GetEmbeddingAsync(string normalizedPrompt, CancellationToken cancellationToken);
}

public interface ILLMProviderResolver
{
    ILLMProvider Resolve(string? requestedProvider);
}

public class LLMProviderResolver(
    IEnumerable<ILLMProvider> providers,
    IOptions<LLMProviderOptions> options) : ILLMProviderResolver
{
    public ILLMProvider Resolve(string? requestedProvider)
    {
        var providerName = string.IsNullOrWhiteSpace(requestedProvider)
            ? options.Value.DefaultProvider
            : requestedProvider.Trim();

        var provider = providers.FirstOrDefault(x =>
            string.Equals(x.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
            throw new InvalidOperationException($"LLM provider '{providerName}' is not configured.");

        return provider;
    }
}
