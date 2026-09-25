# Known pitfalls (all already fixed in the template)

Each of these was hit in practice. If you're debugging an app in this stack, or porting code from the stock
`aichatweb` template, check these first. The "Symptom" lines are what you'll actually see.

## 1. Vector collection key type doesn't match the record
- **Symptom:** blank chat page; log: `The collection's generic key type is 'String', but the key property 'Key' has type 'Guid'`.
- **Cause:** stock template registers `AddSqliteCollection<string, IngestedChunk>` / injects
  `VectorStoreCollection<string, …>` while `IngestedChunk.Key` is `Guid`.
- **Fix:** use `Guid` in both places. SQLite stores Guid keys as TEXT, so existing data is compatible.

## 2. Nullable tool parameters break OllamaSharp
- **Symptom:** first chat message crashes: `JsonException … Path: $.properties.filenameFilter.type … StartArray`.
- **Cause:** `string? param = null` makes the JSON schema `"type": ["string","null"]`; OllamaSharp can't parse a type array.
- **Fix:** non-nullable with a default: `string filenameFilter = ""`. Applies to every `AIFunctionFactory` tool parameter.

## 3. Re-ingestion duplicates chunks
- **Symptom:** chunk count grows each restart (e.g. 16 → 32 → 48); repeated search results.
- **Cause:** `VectorStoreWriterOptions.IncrementalIngestion = false` appends without removing old chunks.
- **Fix:** `IncrementalIngestion = true` (replaces a document's chunks), plus the manifest (#6) so unchanged files aren't re-ingested at all.

## 4. Chunks larger than the embedding model's context
- **Symptom:** search misses text that is clearly in the document; sporadic embed failures
  (`Post "http://127.0.0.1:NNNNN/tokenize": … connectex: No connection could be made`).
- **Cause:** default `MaxTokensPerChunk` is 2000, but `all-minilm` reads only the first 256 tokens; the rest of each
  chunk is never embedded, and Ollama's truncation path is flaky.
- **Fix:** `MaxTokensPerChunk` below the model's context (180 for all-minilm — margin because the chunker counts
  with the gpt-4o tokenizer, not the model's). Larger-context embedders (e.g. nomic-embed-text, 8k) allow bigger chunks.

## 5. Transient Ollama embedding failures fail whole documents
- **Symptom:** upload shows "Indexing failed: … connectex …"; retrying the same file works.
- **Fix:** `RetryingEmbeddingGenerator` (4 attempts, 1/2/4 s backoff) wrapped via
  `AddEmbeddingGenerator(...).Use(...)`. It also covers query embeddings used by search.

## 6. Every restart re-indexes everything; first question takes minutes
- **Cause:** stock template ingests the whole folder on the first `LoadDocuments` call, every run.
- **Fix:** `vector-store.manifest.json` records length + last-write time + chunk size per file; only new/changed
  files are ingested, deleted files' chunks are removed. `DocumentSyncService` starts this at app startup.
  Also, a faulted sync task is no longer cached forever (it's retried on the next search).

## 7. Ollama not running crashes the Blazor circuit
- **Symptom:** "An unhandled error has occurred" bar; in the VS debugger a `NullReferenceException` in
  `Microsoft.AspNetCore.SignalR.ClientProxyExtensions.SendAsync` / `RemoteJSRuntime.EndInvokeDotNet`.
- **Cause:** the chat call throws `HttpRequestException` (connection refused to localhost:11434), which is
  unhandled, so the circuit is torn down; the NRE is the framework replying on the dead circuit — a side effect.
- **Fix:** try/catch around the streaming call in `Chat.razor` showing a banner; `ChatSuggestions` logs instead of
  `DispatchExceptionAsync`. Real remedy for the user: start Ollama.

## 8. Conversation lost when leaving the page
- **Symptom:** "it forgets my previous question" — actually the history vanishes on navigation/reload/restart.
- **Cause:** history lived only in the `Chat` component's fields.
- **Fix:** `ChatHistoryStore` saves JSON per conversation (`conversations/` in the output folder); the id is kept
  in browser `localStorage`. Serialize with `AIJsonUtilities.DefaultOptions` so tool call/result contents round-trip.

## 9. Trimming history can orphan tool results
- When sending only the last N messages, start the window at a **user** message; cutting between an assistant
  function call and its tool result makes the model API reject the request. See `GetMessagesForModel` in `Chat.razor`.

## 10. Environment facts worth knowing
- Ollama Cloud (`*-cloud` models) serves chat only — `/api/embed` returns `unauthorized` for cloud names. Embeddings must be local.
- `gpt-oss:20b-cloud` works but often runs several searches per question; `gpt-oss:120b-cloud` usually needs one.
- `Microsoft.ML.Tokenizers.Data.Cl100kBase` isn't needed: `CreateForModel("gpt-4o")` uses O200kBase.
- Visual Studio may break on first-chance framework exceptions if "Break when thrown" is on for NRE; that's not an app bug by itself.

## 11. Documents that failed to index stay unsearchable until restart
- **Symptom:** app started while Ollama was down → log shows `Succeeded: 'False'`; after Ollama comes back, answers
  still ignore those documents.
- **Cause:** the startup sync *completed* (with failed statuses) rather than *faulting*, so it was never re-run.
- **Fix:** `SemanticSearch` remembers `_lastSyncIncomplete`; the next `LoadDocumentsAsync` (i.e. the next search)
  re-runs the sync, which only ingests the missing/changed files.
