namespace RagChatTemplate.Services.Ingestion;

/// <summary>
/// Indexes new or changed documents in the background at startup, so the first question doesn't have to wait for it.
/// </summary>
public sealed class DocumentSyncService(SemanticSearch semanticSearch, ILogger<DocumentSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Don't hold up app startup
        await Task.Yield();

        try
        {
            await semanticSearch.LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            // Not fatal: the next search retries the sync
            logger.LogWarning(ex, "Background document indexing failed; it will be retried on the next search.");
        }
    }
}
