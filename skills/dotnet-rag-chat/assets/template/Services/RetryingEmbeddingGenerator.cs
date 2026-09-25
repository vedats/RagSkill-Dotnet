using Microsoft.Extensions.AI;

namespace RagChatTemplate.Services;

/// <summary>
/// Retries failed embedding calls. Ollama's local model runner occasionally drops a connection
/// mid-ingestion (e.g. "connectex: No connection could be made"), which would otherwise fail a whole document.
/// </summary>
public sealed class RetryingEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
    ILogger<RetryingEmbeddingGenerator> logger,
    int maxAttempts = 4)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(innerGenerator)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Materialize so the input can be enumerated again on retry
        var inputs = values as IReadOnlyCollection<string> ?? values.ToList();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await base.GenerateAsync(inputs, options, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                logger.LogWarning(ex, "Embedding attempt {Attempt}/{MaxAttempts} failed; retrying in {Delay}s.", attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
