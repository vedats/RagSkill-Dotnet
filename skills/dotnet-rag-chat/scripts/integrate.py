#!/usr/bin/env python3
"""Copy the RAG chat into an existing ASP.NET Core / Blazor project (the mechanical part).

Usage:
    python integrate.py <path-to-project-dir> [--chat-route /chat] [--skip-packages]

What it does (safe to review with git diff afterwards):
  - adds the NuGet packages (same versions as the template)
  - copies Services/, the chat + documents pages, LoadingSpinner, the viewer JS libs and sample docs
  - renames the namespace RagChatTemplate -> the project's root namespace
  - sets the chat page route, adds prerender-free interactive server render mode to both pages
  - copies the markdown renderer as wwwroot/rag-chat.js (the host may have its own app.js)
  - appends the few global CSS rules the chat needs, adds @usings and the Chat config section

What it does NOT do (edit these by hand, see references/integrate-existing.md):
  Program.cs registrations, <script> tag in App.razor, nav menu links, auth.
It refuses to overwrite files that already exist in the target.
"""
import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "template"
COPY = [
    "Services",
    "Components/Pages/Chat",
    "Components/Pages/Documents.razor",
    "Components/Pages/Documents.razor.css",
    "Components/Layout/LoadingSpinner.razor",
    "Components/Layout/LoadingSpinner.razor.css",
    "wwwroot/lib/pdf_viewer",
    "wwwroot/lib/markdown_viewer",
    "wwwroot/lib/marked",
    "wwwroot/lib/dompurify",
    "wwwroot/lib/pdfjs-dist",
    "wwwroot/Data",
]
CSS_SELECTORS = [".main-background-gradient", ".btn-default", ".btn-subtle", ".page-width"]


def root_namespace(csproj: Path) -> str:
    m = re.search(r"<RootNamespace>([^<]+)</RootNamespace>", csproj.read_text(encoding="utf-8-sig"))
    return m.group(1) if m else csproj.stem


def global_css_rules() -> str:
    css = (TEMPLATE / "wwwroot" / "app.css").read_text(encoding="utf-8-sig")
    rules = []
    for sel in CSS_SELECTORS:
        # the template combines "html, .main-background-gradient"; only take the class so the host's html isn't restyled
        pattern = r"(?:html, )?" + re.escape(sel) + r"\s*\{[^}]*\}(?:\s*\n\s+" + re.escape(sel) + r":hover\s*\{[^}]*\})?"
        for m in re.finditer(pattern, css):
            rules.append(m.group(0).replace("html, ", ""))
    return "\n\n/* RAG chat (dotnet-rag-chat skill) */\n" + "\n\n".join(rules) + "\n"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("project_dir")
    ap.add_argument("--chat-route", default=None, help='Route for the chat page (default "/chat", or "/" if free)')
    ap.add_argument("--skip-packages", action="store_true")
    args = ap.parse_args()

    target = Path(args.project_dir).resolve()
    csprojs = list(target.glob("*.csproj"))
    if len(csprojs) != 1:
        sys.exit(f"error: expected exactly one .csproj in {target}, found {len(csprojs)}")
    csproj = csprojs[0]
    ns = root_namespace(csproj)

    conflicts = [c for c in COPY if (target / c).exists() and c != "wwwroot/Data"]
    if conflicts:
        sys.exit("error: these already exist in the target, not overwriting: " + ", ".join(conflicts))

    # Route: "/" if no page claims it
    route = args.chat_route
    if route is None:
        taken = any(re.search(r'@page\s+"/"', p.read_text(encoding="utf-8-sig", errors="ignore"))
                    for p in target.rglob("*.razor") if "bin" not in p.parts and "obj" not in p.parts)
        route = "/chat" if taken else "/"
    documents_taken = any(re.search(r'@page\s+"/documents"', p.read_text(encoding="utf-8-sig", errors="ignore"))
                          for p in target.rglob("*.razor") if "bin" not in p.parts and "obj" not in p.parts)
    if documents_taken:
        sys.exit('error: the target already has a page at "/documents"; rename it or adapt Documents.razor by hand')

    if not args.skip_packages:
        tpl = (TEMPLATE / "RagChatTemplate.csproj").read_text(encoding="utf-8")
        for name, version in re.findall(r'<PackageReference Include="([^"]+)" Version="([^"]+)"', tpl):
            print(f"adding package {name} {version}")
            subprocess.run(["dotnet", "add", str(csproj), "package", name, "--version", version],
                           check=True, stdout=subprocess.DEVNULL)

    for rel in COPY:
        src, dst = TEMPLATE / rel, target / rel
        if src.is_dir():
            shutil.copytree(src, dst, dirs_exist_ok=True)
        else:
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)

    for f in [*(target / "Services").rglob("*.cs"), *(target / "Components/Pages/Chat").glob("*.razor"),
              target / "Components/Pages/Documents.razor", target / "Components/Layout/LoadingSpinner.razor"]:
        s = f.read_text(encoding="utf-8-sig")
        f.write_text(s.replace("RagChatTemplate", ns), encoding="utf-8")

    render = '@rendermode @(new InteractiveServerRenderMode(prerender: false))\n'
    chat = target / "Components/Pages/Chat/Chat.razor"
    chat.write_text(chat.read_text(encoding="utf-8").replace('@page "/"\n', f'@page "{route}"\n{render}', 1), encoding="utf-8")
    docs = target / "Components/Pages/Documents.razor"
    s = docs.read_text(encoding="utf-8").replace('@page "/documents"\n', f'@page "/documents"\n{render}', 1)
    docs.write_text(s.replace('href="/"', f'href="{route.lstrip("/") or "/"}"'), encoding="utf-8")

    shutil.copy2(TEMPLATE / "wwwroot" / "app.js", target / "wwwroot" / "rag-chat.js")
    with open(target / "wwwroot" / "app.css", "a", encoding="utf-8") as f:
        f.write(global_css_rules())

    imports = target / "Components" / "_Imports.razor"
    s = imports.read_text(encoding="utf-8-sig") if imports.exists() else ""
    for u in ["@using Microsoft.Extensions.AI", "@using Microsoft.JSInterop", f"@using {ns}.Services"]:
        if u not in s:
            s = s.rstrip() + "\n" + u + "\n"
    imports.write_text(s, encoding="utf-8")

    settings = target / "appsettings.json"
    cfg = json.loads(settings.read_text(encoding="utf-8-sig")) if settings.exists() else {}
    cfg.setdefault("Chat", {"MaxHistoryMessages": 10})
    settings.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")

    print(f"\nCopied RAG chat into {target} (namespace {ns}, chat at {route}, documents at /documents).")
    print("Now do the manual steps in references/integrate-existing.md: Program.cs, rag-chat.js <script> in App.razor, nav links.")


if __name__ == "__main__":
    main()
