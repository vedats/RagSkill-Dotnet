using System.Text.Json;
using RagChatTemplate.Services.Ingestion;
using Microsoft.Extensions.VectorData;

namespace RagChatTemplate.Services;

public record DocumentIngestionStatus(string DocumentId, bool Succeeded, Exception? Exception = null);

public class SemanticSearch(
    VectorStoreCollection<Guid, IngestedChunk> vectorCollection,
    [FromKeyedServices("ingestion_directory")] DirectoryInfo ingestionDirectory,
    [FromKeyedServices("ingestion_manifest")] FileInfo manifestFile,
    DataIngestor dataIngestor,
    ILogger<SemanticSearch> logger)
{
    public static readonly string[] SupportedExtensions = [".pdf", ".md"];

    private readonly SemaphoreSlim _ingestionLock = new(1, 1);
    private readonly Lock _syncLock = new();
    private Task? _syncTask;
    private volatile bool _lastSyncIncomplete;

    public DirectoryInfo IngestionDirectory => ingestionDirectory;

    public IReadOnlyList<FileInfo> GetDocuments()
    {
        ingestionDirectory.Refresh();
        if (!ingestionDirectory.Exists)
        {
            return [];
        }

        return ingestionDirectory.EnumerateFiles()
            .Where(f => SupportedExtensions.Contains(f.Extension.ToLowerInvariant()))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Brings the vector store in line with the data directory. Runs once (started at app startup);
    /// only new or changed files are ingested, so restarts are fast. A run that failed, or left some documents
    /// unindexed (e.g. Ollama was down), is retried on the next call; already-indexed files are skipped.
    /// </summary>
    public Task LoadDocumentsAsync()
    {
        lock (_syncLock)
        {
            if (_syncTask is null || _syncTask.IsFaulted || _syncTask.IsCanceled
                || (_syncTask.IsCompletedSuccessfully && _lastSyncIncomplete))
            {
                _syncTask = SyncDocumentsAsync();
            }
            return _syncTask;
        }
    }

    public async Task<IReadOnlyList<DocumentIngestionStatus>> IngestFilesAsync(IEnumerable<FileInfo> files)
    {
        // The initial sync may already have picked these files up; anything unchanged since then is skipped
        await LoadDocumentsAsync();
        return await IngestChangedAsync(files.ToList());
    }

    /// <summary>Deletes a document's file and removes its chunks from the index, so it can no longer be found or cited.</summary>
    public async Task DeleteDocumentAsync(string fileName)
    {
        // Only allow plain file names of supported types inside the data directory
        if (fileName != Path.GetFileName(fileName) || !SupportedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
        {
            throw new ArgumentException($"'{fileName}' is not a valid document name.", nameof(fileName));
        }

        // Waits for any running indexing, so a document can't be re-added right after it's deleted
        await _ingestionLock.WaitAsync();
        try
        {
            var file = new FileInfo(Path.Combine(ingestionDirectory.FullName, fileName));
            if (file.Exists)
            {
                file.Delete();
            }

            await DeleteChunksAsync(fileName);

            var manifest = await LoadManifestAsync();
            manifest.Remove(fileName);
            await SaveManifestAsync(manifest);

            logger.LogInformation("Deleted document '{id}'.", fileName);
        }
        finally
        {
            _ingestionLock.Release();
        }
    }

    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(string text, string? documentIdFilter, int maxResults)
    {
        // Ensure documents have been loaded before searching
        await LoadDocumentsAsync();

        var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
        {
            Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
        });

        return await nearest.Select(result => result.Record).ToListAsync();
    }

    private async Task SyncDocumentsAsync()
    {
        var statuses = await IngestChangedAsync(GetDocuments(), removeMissing: true);
        _lastSyncIncomplete = statuses.Any(s => !s.Succeeded);
    }

    private async Task<IReadOnlyList<DocumentIngestionStatus>> IngestChangedAsync(IReadOnlyList<FileInfo> files, bool removeMissing = false)
    {
        await _ingestionLock.WaitAsync();
        try
        {
            var manifest = await LoadManifestAsync();
            var statuses = new List<DocumentIngestionStatus>();
            var toIngest = new List<FileInfo>();

            foreach (var file in files)
            {
                file.Refresh();
                if (manifest.TryGetValue(file.Name, out var entry) && entry == ManifestEntry.For(file) && await HasChunksAsync(file.Name))
                {
                    statuses.Add(new(file.Name, Succeeded: true));
                }
                else
                {
                    toIngest.Add(file);
                }
            }

            if (removeMissing)
            {
                var present = files.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var removed in manifest.Keys.Where(id => !present.Contains(id)).ToList())
                {
                    await DeleteChunksAsync(removed);
                    manifest.Remove(removed);
                    logger.LogInformation("Removed '{id}' from the index because the file no longer exists.", removed);
                }
            }

            logger.LogInformation("Indexing {count} new or changed document(s); {skipped} unchanged.", toIngest.Count, statuses.Count);
            if (toIngest.Count > 0)
            {
                var results = await dataIngestor.IngestFilesAsync(ingestionDirectory, toIngest);
                foreach (var file in toIngest)
                {
                    var result = results.FirstOrDefault(r => string.Equals(r.DocumentId, file.Name, StringComparison.OrdinalIgnoreCase));
                    if (result is { Succeeded: true })
                    {
                        manifest[file.Name] = ManifestEntry.For(file);
                        statuses.Add(new(file.Name, Succeeded: true));
                    }
                    else
                    {
                        manifest.Remove(file.Name);
                        statuses.Add(new(file.Name, Succeeded: false, result?.Exception));
                    }
                }
            }

            await SaveManifestAsync(manifest);
            return statuses;
        }
        finally
        {
            _ingestionLock.Release();
        }
    }

    private async Task<bool> HasChunksAsync(string documentId)
    {
        // Guards against a manifest that outlived its vector store (e.g. the database file was deleted)
        if (!await vectorCollection.CollectionExistsAsync())
        {
            return false;
        }
        return await vectorCollection.GetAsync(r => r.DocumentId == documentId, top: 1).AnyAsync();
    }

    private async Task DeleteChunksAsync(string documentId)
    {
        if (!await vectorCollection.CollectionExistsAsync())
        {
            return;
        }
        var keys = await vectorCollection.GetAsync(r => r.DocumentId == documentId, top: int.MaxValue).Select(r => r.Key).ToListAsync();
        await vectorCollection.DeleteAsync(keys);
    }

    private async Task<Dictionary<string, ManifestEntry>> LoadManifestAsync()
    {
        manifestFile.Refresh();
        if (!manifestFile.Exists)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = manifestFile.OpenRead();
            var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, ManifestEntry>>(stream);
            return new(entries ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ingestion manifest is corrupt; all documents will be re-indexed.");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveManifestAsync(Dictionary<string, ManifestEntry> manifest)
    {
        await using var stream = File.Create(manifestFile.FullName);
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    // Identifies a file version; if either value changes the file is re-indexed.
    // MaxTokensPerChunk is included so changing the chunk size re-indexes everything.
    private sealed record ManifestEntry(long Length, DateTime LastWriteTimeUtc, int MaxTokensPerChunk)
    {
        public static ManifestEntry For(FileInfo file) => new(file.Length, file.LastWriteTimeUtc, IngestedChunk.MaxTokensPerChunk);
    }
}
