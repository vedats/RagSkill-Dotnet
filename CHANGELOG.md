# Changelog

## 1.0.0 — 2026-09-25

First public release.

- `scaffold.py`: create a new RAG chat app (name, chat model, embedding model/dimensions, chunk size).
- `integrate.py` + guide: add the RAG chat to an existing ASP.NET Core / Blazor project (tested on `dotnet new blazor`).
- Template app: PDF/Markdown upload and delete, citations with PDF/Markdown viewers, document list on the start page,
  incremental indexing with a manifest, background indexing at startup, retry for flaky embeddings, friendly error
  when Ollama is down, persistent chat history, last-N-messages window.
- `references/pitfalls.md`: 11 real failures of this stack with symptoms, causes and fixes.
