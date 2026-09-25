using System.Text.Json;
using Microsoft.Extensions.AI;

namespace RagChatTemplate.Services;

/// <summary>
/// Saves chat conversations to disk so they survive leaving the chat page, page reloads and app restarts.
/// Each conversation is one JSON file named by its id; the system prompt isn't stored.
/// </summary>
public class ChatHistoryStore(string directory, ILogger<ChatHistoryStore> logger)
{
    public async Task<List<ChatMessage>> LoadAsync(Guid conversationId)
    {
        var path = GetPath(conversationId);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<ChatMessage>>(stream, AIJsonUtilities.DefaultOptions) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Conversation '{id}' could not be read; starting a new one.", conversationId);
            return [];
        }
    }

    public async Task SaveAsync(Guid conversationId, IEnumerable<ChatMessage> messages)
    {
        Directory.CreateDirectory(directory);
        var path = GetPath(conversationId);

        // Write to a temp file and swap it in, so a crash mid-write can't corrupt the saved conversation
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, messages.Where(m => m.Role != ChatRole.System).ToList(), AIJsonUtilities.DefaultOptions);
        }
        File.Move(tempPath, path, overwrite: true);
    }

    public void Delete(Guid conversationId)
    {
        var path = GetPath(conversationId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPath(Guid conversationId) => Path.Combine(directory, $"{conversationId:N}.json");
}
