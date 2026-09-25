# Adding the RAG chat to an existing project

Works for ASP.NET Core projects on net10.0 that use (or can add) Blazor Server interactivity — e.g. anything
created with `dotnet new blazor`. For Web API-only, MVC or WASM-only projects, scaffold a separate app with
`scripts/scaffold.py` instead and link to it; that's far less invasive.

Tested on a stock `dotnet new blazor --interactivity Server` app (Bootstrap, NavMenu layout, Home at `/`,
`MapStaticAssets`, per-page render modes). Commit or back up the target first so the change is easy to review.

## 1. Run the integration script (mechanical part)

```bash
python <skill-dir>/scripts/integrate.py <project-dir>
```

It adds the packages, copies the services/pages/viewer libs/sample docs, renames the namespace, picks the chat
route (`/` if free, otherwise `/chat`; override with `--chat-route`), adds
`@rendermode @(new InteractiveServerRenderMode(prerender: false))` to both pages, copies the markdown renderer as
`wwwroot/rag-chat.js`, appends the global CSS rules the chat uses, and adds `@using`s and the `Chat` config
section. It refuses to overwrite existing files or clash with an existing `/documents` page.

## 2. Program.cs (by hand)

Add the usings `Microsoft.Extensions.AI`, `OllamaSharp`, `<Ns>.Services`, `<Ns>.Services.Ingestion`, then copy the
template's `Program.cs` block that starts at `// Chat model (must support tool calling)` and ends before
`var app = builder.Build();` (chat client, embedding generator + retry, vector store, collection with a **Guid**
key, `DataIngestor`, `SemanticSearch`, `ChatHistoryStore`, `DocumentSyncService`, the two keyed singletons).
Keep the host's own `AddRazorComponents().AddInteractiveServerComponents()`.

Static files: add `app.UseStaticFiles();` (before `app.MapStaticAssets();` if present). `MapStaticAssets` only
serves files that existed at build time, so without `UseStaticFiles` uploaded documents 404 and citation links
break. Verified: with both, `/Data/<uploaded file>` returns 200.

## 3. App.razor (by hand) — easy to miss

Add, before the `blazor.web.js` script:

```html
<script src="@Assets["rag-chat.js"]" type="module"></script>
```

It defines the `<assistant-message>` element that renders answers from Markdown. Without it the chat shows only
the citation cards and **no answer text**, with no error anywhere.

## 4. Navigation (by hand)

Add links to the chat route and `documents` wherever the host keeps navigation (e.g. `Components/Layout/NavMenu.razor`).

## 5. Security

If the host has authentication, add `@attribute [Authorize]` to `Documents.razor` at least — uploads and
deletes are otherwise open to anyone. If it has none, tell the user.

## 6. Verify

`dotnet build` (0 errors/warnings), run, then check: log shows `Indexing … document(s)` and `Completed processing …
Succeeded: 'True'`; ask a question on the chat page and confirm the **answer text** and a citation appear; upload
and delete a small `.md` on `/documents`; open a couple of the host's existing pages to confirm nothing broke.

## Notes from the test integration

- The chat inherits the host layout (sidebar etc.) and looks fine inside it; its CSS is scoped, and the link
  colors/underlines are reset explicitly so Bootstrap's link styles don't leak in.
- The template's `wwwroot/lib/tailwindcss` (preflight reset) is deliberately not copied: it would restyle the host.
- The header shows the project name as the page title (`ChatHeader.razor`); change it if the host wants a different one.
