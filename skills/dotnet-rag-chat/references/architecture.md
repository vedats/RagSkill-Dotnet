# Architecture of the template

## Request flow

**Indexing** (startup + uploads):
`wwwroot/Data/*.pdf|*.md` → `DocumentReader` (PdfPig for PDF, Markdig reader for MD) →
`SemanticSimilarityChunker` (≤ `MaxTokensPerChunk` tokens) → embeddings via Ollama (`all-minilm`, local) →
`VectorStoreWriter` → SQLite + sqlite-vec (`vector-store.db`, collection `data-<project>-chunks`).

**Answering a question**:
`Chat.razor` sends system prompt + last N messages + the `Search` tool → chat model (Ollama) decides to call
`Search(searchPhrase, filenameFilter)` → `SemanticSearch.SearchAsync` embeds the phrase and returns the top 5 chunks
as `<result filename="…">text</result>` → the model answers and appends `<citation filename='…'>quote</citation>`
tags → `ChatMessageItem` renders them as `ChatCitation` links that open the PDF/Markdown viewer at the quote.
This is "agentic RAG" (the model chooses when to search), which costs two model round trips per question.

## File map

| File | Role |
|---|---|
| `Program.cs` | DI: chat client (+function invocation, logging), embedding generator (+retry), vector store/collection, services, keyed `ingestion_directory` and `ingestion_manifest` |
| `Services/IngestedChunk.cs` | Vector record (Guid key, document id, text, 384-d vector); constants `VectorDimensions`, `MaxTokensPerChunk`, `CollectionName` |
| `Services/SemanticSearch.cs` | Document list, sync (manifest-based incremental indexing), upload ingestion, delete, vector search. Single `SemaphoreSlim` serializes all index writes |
| `Services/Ingestion/DataIngestor.cs` | Builds the DataIngestion pipeline (reader → chunker → writer, `IncrementalIngestion = true`) |
| `Services/Ingestion/DocumentReader.cs` | Maps file extension/media type to a reader; relative path = document id |
| `Services/Ingestion/PdfPigReader.cs` | PDF → sections/paragraphs using PdfPig layout analysis |
| `Services/Ingestion/DocumentSyncService.cs` | `BackgroundService` that starts the sync at startup |
| `Services/RetryingEmbeddingGenerator.cs` | Retries transient embedding failures |
| `Services/ChatHistoryStore.cs` | Saves/loads conversations as JSON (`conversations/<id>.json`) |
| `Components/Pages/Chat/Chat.razor` | Chat page: system prompt, `Search` tool, streaming, error banner, history restore/save, last-N window, start-screen document list |
| `Components/Pages/Chat/ChatMessageItem.razor` | Renders user/assistant messages and parses citation tags |
| `Components/Pages/Chat/ChatCitation.razor` | Link to `lib/pdf_viewer` / `lib/markdown_viewer` with the quote highlighted |
| `Components/Pages/Chat/ChatSuggestions.razor` | Follow-up question suggestions (second model call; failures are only logged) |
| `Components/Pages/Documents.razor` | Upload (validation, temp file then move, per-file status) and delete (inline confirm) |
| `wwwroot/lib/*` | PDF.js viewer, markdown viewer, marked, DOMPurify, Tailwind preflight |
| `appsettings.json` | `Chat:MaxHistoryMessages` (default 10) |

Runtime files live in the output folder (`AppContext.BaseDirectory`, e.g. `bin/Debug/net10.0/`):
`vector-store.db`, `vector-store.manifest.json`, `conversations/`. Deleting all three resets the app's state
(documents in `wwwroot/Data` are kept and re-indexed).

## Extending

- **New file type** (e.g. `.txt`, `.docx`): add the extension to `SemanticSearch.SupportedExtensions` and the
  upload `accept` attribute in `Documents.razor`; in `DocumentReader`, map the extension to a media type in
  `GetCustomMediaType` and route it to a reader. TXT can reuse the Markdown reader; DOCX needs a reader
  (DocumentFormat.OpenXml) that emits `IngestionDocumentParagraph`s like `PdfPigReader` does.
- **Different embedding model**: change `Program.cs` + `VectorDimensions`, delete the index files. Bigger-context
  models allow a larger `MaxTokensPerChunk`.
- **Retrieval before the model call** (one round trip instead of two): call `SemanticSearch.SearchAsync` in
  `AddUserMessageAsync`, put the results into the prompt, and drop the `Search` tool. Faster and works with
  models without tool support, but the model can no longer refine its own queries.
- **Non-Ollama providers**: swap `OllamaApiClient` for any `IChatClient`/`IEmbeddingGenerator` implementation
  (e.g. `Microsoft.Extensions.AI.OpenAI`); nothing else depends on Ollama except error texts.
