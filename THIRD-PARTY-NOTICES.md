# Third-party notices

This repository includes or is derived from the following third-party works. Each remains under its own license;
the license headers inside the bundled files are preserved.

## Derived code

| Component | Where | License |
|---|---|---|
| .NET "AI Chat Web App" template (`aichatweb`, package `Microsoft.Extensions.AI.Templates`), © .NET Foundation and Contributors — [dotnet/extensions](https://github.com/dotnet/extensions) | `skills/dotnet-rag-chat/assets/template/` started from this template and was modified extensively (bug fixes, documents page, incremental indexing, chat persistence, …). The two sample documents in `wwwroot/Data/` and the `*.razor.js` / `app.js` files (which keep Microsoft's signature blocks) come from the template unchanged. | MIT |

## Bundled client libraries (`skills/dotnet-rag-chat/assets/template/wwwroot/lib/`)

| Library | Version | License | Source |
|---|---|---|---|
| PDF.js (`pdfjs-dist`), © Mozilla Foundation | 4.10.38 | Apache-2.0 | https://github.com/mozilla/pdf.js |
| marked, © Christopher Jeffrey | 15.0.6 | MIT | https://github.com/markedjs/marked |
| DOMPurify, © Cure53 and contributors | 3.4.13 | Apache-2.0 or MPL-2.0 | https://github.com/cure53/DOMPurify |
| Tailwind CSS (`preflight.css` only), © Tailwind Labs | 4.0.3 | MIT | https://github.com/tailwindlabs/tailwindcss |

`pdf_viewer/` and `markdown_viewer/` are small viewer pages from the .NET template (MIT) built on the libraries above.

## NuGet packages (referenced, not redistributed)

Generated projects reference these packages; they are downloaded from nuget.org under their own licenses:
OllamaSharp (MIT), Microsoft.Extensions.AI (MIT), Microsoft.Extensions.DataIngestion and
Microsoft.Extensions.DataIngestion.Markdig (MIT), PdfPig (Apache-2.0), Microsoft.ML.Tokenizers.Data.O200kBase (MIT),
CommunityToolkit.VectorData.SqliteVec (MIT).
