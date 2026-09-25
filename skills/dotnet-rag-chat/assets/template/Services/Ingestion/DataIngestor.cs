using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;

namespace RagChatTemplate.Services.Ingestion;

public class DataIngestor(
    ILogger<DataIngestor> logger,
    ILoggerFactory loggerFactory,
    VectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public async Task<IReadOnlyList<IngestionResult>> IngestFilesAsync(DirectoryInfo directory, IEnumerable<FileInfo> files)
    {
        using var writer = CreateWriter();
        using var pipeline = CreatePipeline(directory, writer);

        var results = new List<IngestionResult>();
        await foreach (var result in pipeline.ProcessAsync(files))
        {
            logger.LogInformation("Completed processing '{id}'. Succeeded: '{succeeded}'.", result.DocumentId, result.Succeeded);
            results.Add(result);
        }
        return results;
    }

    // Incremental ingestion replaces a document's existing chunks, so re-ingesting a file doesn't create duplicates
    private VectorStoreWriter<string> CreateWriter() => new(vectorStore, dimensionCount: IngestedChunk.VectorDimensions, new()
    {
        CollectionName = IngestedChunk.CollectionName,
        DistanceFunction = IngestedChunk.VectorDistanceFunction,
        IncrementalIngestion = true,
    });

    private IngestionPipeline<string> CreatePipeline(DirectoryInfo directory, VectorStoreWriter<string> writer) => new(
        reader: new DocumentReader(directory),
        chunker: new SemanticSimilarityChunker(embeddingGenerator, new(TiktokenTokenizer.CreateForModel("gpt-4o"))
        {
            MaxTokensPerChunk = IngestedChunk.MaxTokensPerChunk,
        }),
        writer: writer,
        loggerFactory: loggerFactory);
}
