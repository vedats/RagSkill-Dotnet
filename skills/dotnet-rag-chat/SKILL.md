---
name: dotnet-rag-chat
description: Scaffold a working "chat with your documents" RAG app in .NET 10 Blazor Server (Ollama for chat + local embeddings, SQLite vector store, Microsoft.Extensions.AI/DataIngestion), or add that RAG chat to an existing ASP.NET Core/Blazor project. Includes document upload/delete, citations, incremental indexing, persistent chat history and fixes for known Ollama/SqliteVec pitfalls. Use this whenever the user wants a RAG app, document Q&A, "belgelerimle sohbet", "PDF'lerime soru sormak", a knowledge-base chatbot, AnythingLLM-like app, or semantic search over files in C#/.NET/Blazor — even if they don't say "RAG" — and also when debugging such an app (Ollama errors, embeddings, vector store, chunking, slow first question).
---

# .NET RAG Chat (Blazor + Ollama + SQLite vectors)

This skill packages a tested, working RAG chat app so a new project starts from something that already runs,
instead of from the stock `aichatweb` template, which has several bugs that took real debugging to find
(see `references/pitfalls.md`).

What the app does: users upload PDF/Markdown files → text is chunked and embedded locally → stored in a
SQLite vector DB → questions are answered by an Ollama (cloud or local) chat model that searches the docs via a
tool call and cites sources. Extras: documents page (upload/delete), document list on the start page, only
new/changed files are re-indexed, background indexing at startup, chat history saved per browser and restored,
last-N-messages window, retries for flaky embeddings, friendly error when Ollama is down.

Talk to the user in the language they're using.

## Decide the mode

1. **New app** (most common): use the scaffold script — step "Create a new app".
2. **Add to an existing ASP.NET Core / Blazor project**: run `scripts/integrate.py <project-dir>` for the
   mechanical part, then do the manual steps (Program.cs, a `<script>` tag in App.razor, nav links) in
   `references/integrate-existing.md`. Read that file first — the App.razor script tag is easy to miss and
   without it answers silently don't render.
3. **Questions / debugging an app built from this**: `references/architecture.md` explains every file;
   `references/pitfalls.md` lists known failures and their fixes — check it before debugging from scratch.

## Create a new app

Prerequisites (check, don't assume): .NET 10 SDK (`dotnet --version`), Python 3, Ollama installed and running
(`curl http://localhost:11434/api/version`). Cloud chat models (`*-cloud`) need the Ollama app signed in.

```bash
python <skill-dir>/scripts/scaffold.py <ProjectName> --output <target-dir>
```

Options: `--chat-model` (default `gpt-oss:120b-cloud`; must support tools), `--embedding-model` (default
`all-minilm`), `--embedding-dimensions` (default 384 — must match the embedding model), `--max-tokens-per-chunk`
(default 180 — keep below the embedding model's context window), `--no-examples` (skip the two sample docs).
The script refuses a non-empty target directory, so it can't overwrite work.

Then:
1. `ollama pull <embedding-model>` — Ollama Cloud has no embedding models, embeddings are always local.
   Verify the size: `curl -s localhost:11434/api/embed -d '{"model":"<m>","input":"hi"}'` → vector length must equal
   `--embedding-dimensions`.
2. If the chat model is local (not `-cloud`), `ollama pull` it too, and confirm it lists `tools` in
   `curl -s localhost:11434/api/tags` capabilities — the app relies on tool calling.
3. `dotnet build` in the target dir; expect 0 errors, 0 warnings.
4. Run it and verify end to end (don't stop at "it builds" — most bugs in this stack only show up at runtime):
   start with `dotnet run`, wait for the log line `Indexing N new or changed document(s)` and the
   `Completed processing` lines, open the page, ask a question about a sample doc, and confirm the answer has a
   citation. Check the log for `fail:` lines.

First start indexes everything in the background (≈1 min per MB of PDF with all-minilm); later starts skip
unchanged files, so questions are answered in ~2 s.

## Customizing

Common changes and where they live (details in `references/architecture.md`):

| Change | Where |
|---|---|
| Chat / embedding model | `Program.cs` (and `VectorDimensions` in `Services/IngestedChunk.cs` if the embedding size changes) |
| Chunk size | `MaxTokensPerChunk` in `Services/IngestedChunk.cs` |
| How many past messages go to the model | `Chat:MaxHistoryMessages` in `appsettings.json` |
| System prompt / answer rules | `SystemPrompt` in `Components/Pages/Chat/Chat.razor` |
| Supported file types | `SupportedExtensions` in `Services/SemanticSearch.cs` + a reader in `Services/Ingestion/DocumentReader.cs` |
| Upload limits | `MaxFileSize` / `MaxFileCount` in `Components/Pages/Documents.razor` |

Changing the embedding model or chunk size invalidates existing vectors. Chunk size is tracked in the index
manifest and triggers re-indexing automatically; for an embedding-model change, delete `vector-store.db` and
`vector-store.manifest.json` in the output folder (`bin/Debug/net10.0/`) so everything is re-embedded.

## Things to tell the user

- There is no authentication: anyone who can reach the app can upload/delete documents. Fine locally; add auth
  before exposing it.
- Ollama must be running; if it isn't, the chat shows "Couldn't reach Ollama…" instead of crashing.
- Three packages are previews (`CommunityToolkit.VectorData.SqliteVec`, `Microsoft.Extensions.DataIngestion*`):
  pin versions and re-test after upgrades.
