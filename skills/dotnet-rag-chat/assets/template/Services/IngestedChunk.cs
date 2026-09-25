using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

namespace RagChatTemplate.Services;

public class IngestedChunk
{
    public const int VectorDimensions = 384; // 384 is the default vector size for the all-minilm embedding model
    // all-minilm only reads the first 256 tokens of its input; stay below that (with margin for tokenizer differences)
    // so the whole chunk is embedded and Ollama never has to truncate
    public const int MaxTokensPerChunk = 180;
    public const string VectorDistanceFunction = DistanceFunction.CosineDistance;
    public const string CollectionName = "data-ragchattemplate-chunks";

    [VectorStoreKey(StorageName = "key")]
    [JsonPropertyName("key")]
    public required Guid Key { get; set; }

    [VectorStoreData(StorageName = "documentid")]
    [JsonPropertyName("documentid")]
    public required string DocumentId { get; set; }

    [VectorStoreData(StorageName = "content")]
    [JsonPropertyName("content")]
    public required string Text { get; set; }

    [VectorStoreData(StorageName = "context")]
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [VectorStoreVector(VectorDimensions, DistanceFunction = VectorDistanceFunction, StorageName = "embedding")]
    [JsonPropertyName("embedding")]
    public string? Vector => Text;
}
