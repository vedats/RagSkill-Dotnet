# RagChatTemplate — chat with your documents

A .NET 10 Blazor Server RAG app: upload PDF/Markdown files, ask questions, get answers with source citations.

- Chat model: Ollama (`gpt-oss:120b-cloud` by default — cloud models need the Ollama app signed in)
- Embeddings: local Ollama model (`all-minilm`)
- Vector store: SQLite + sqlite-vec (`vector-store.db` next to the app binaries)

## Run

```sh
ollama pull all-minilm
dotnet run
```

Open the URL shown in the console. The first start indexes `wwwroot/Data` in the background; later starts only
index new or changed files. Manage documents on the **Documents** page (upload / delete).

## Configure

| Setting | Where |
|---|---|
| Chat / embedding model | `Program.cs` (+ `VectorDimensions` in `Services/IngestedChunk.cs`) |
| Chunk size | `MaxTokensPerChunk` in `Services/IngestedChunk.cs` |
| Messages sent to the model | `Chat:MaxHistoryMessages` in `appsettings.json` |
| System prompt | `Components/Pages/Chat/Chat.razor` |

There is no authentication — anyone who can reach the app can upload and delete documents.
