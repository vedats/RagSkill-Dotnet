#!/usr/bin/env python3
"""Create a new RAG chat app from the bundled template.

Usage:
    python scaffold.py MyDocsChat --output F:/Projects/MyDocsChat
    python scaffold.py MyDocsChat --output ./MyDocsChat --chat-model gpt-oss:20b-cloud
    python scaffold.py MyDocsChat --output ./MyDocsChat --embedding-model nomic-embed-text --embedding-dimensions 768

The template project is named "RagChatTemplate"; every occurrence is replaced with the new name,
the vector collection gets its own name, and the project gets a fresh UserSecretsId and dev ports.
"""
import argparse
import random
import re
import shutil
import sys
import uuid
from pathlib import Path

TEMPLATE_NAME = "RagChatTemplate"
TEMPLATE_SLUG = "ragchattemplate"
TEXT_SUFFIXES = {".cs", ".razor", ".css", ".js", ".json", ".csproj", ".slnx", ".md", ".html"}
TEMPLATE_DIR = Path(__file__).resolve().parent.parent / "assets" / "template"


def replace_in_file(path: Path, replacements):
    text = path.read_text(encoding="utf-8-sig")
    new = text
    for old, repl in replacements:
        new = re.sub(old, repl, new) if isinstance(old, re.Pattern) else new.replace(old, repl)
    if new != text:
        path.write_text(new, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("name", help="Project/namespace name, e.g. MyDocsChat (letters, digits, _ and .)")
    parser.add_argument("--output", help="Target directory (default: ./<name>). Must not exist or be empty.")
    parser.add_argument("--chat-model", default="gpt-oss:120b-cloud", help="Ollama chat model (needs tool support)")
    parser.add_argument("--embedding-model", default="all-minilm", help="Local Ollama embedding model")
    parser.add_argument("--embedding-dimensions", type=int, default=384, help="Vector size of the embedding model")
    parser.add_argument("--max-tokens-per-chunk", type=int, default=180,
                        help="Keep below the embedding model's context (all-minilm: 256)")
    parser.add_argument("--no-examples", action="store_true", help="Don't copy the example documents")
    args = parser.parse_args()

    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*", args.name):
        sys.exit(f"error: '{args.name}' is not a valid C# namespace/project name")

    output = Path(args.output or args.name).resolve()
    if output.exists() and any(output.iterdir()):
        sys.exit(f"error: {output} already exists and is not empty; choose another --output")

    shutil.copytree(TEMPLATE_DIR, output, dirs_exist_ok=True)
    (output / f"{TEMPLATE_NAME}.csproj").rename(output / f"{args.name}.csproj")
    (output / f"{TEMPLATE_NAME}.slnx").rename(output / f"{args.name}.slnx")

    if args.no_examples:
        for f in (output / "wwwroot" / "Data").glob("*"):
            f.unlink()

    slug = re.sub(r"[^a-z0-9]+", "-", args.name.lower()).strip("-")
    http_port = random.randint(5100, 5999)
    https_port = random.randint(7100, 7999)
    common = [
        (TEMPLATE_NAME, args.name),
        (f"data-{TEMPLATE_SLUG}-chunks", f"data-{slug}-chunks"),
    ]

    for path in output.rglob("*"):
        if not path.is_file() or path.suffix not in TEXT_SUFFIXES or "lib" in path.relative_to(output).parts:
            continue
        replace_in_file(path, common)

    replace_in_file(output / f"{args.name}.csproj", [
        (re.compile(r"<UserSecretsId>[^<]*</UserSecretsId>"), f"<UserSecretsId>{uuid.uuid4()}</UserSecretsId>"),
    ])
    replace_in_file(output / "Properties" / "launchSettings.json", [
        (re.compile(r"localhost:5\d{3}"), f"localhost:{http_port}"),
        (re.compile(r"localhost:7\d{3}"), f"localhost:{https_port}"),
    ])
    replace_in_file(output / "Program.cs", [
        ('"gpt-oss:120b-cloud"', f'"{args.chat_model}"'),
        ('"all-minilm"', f'"{args.embedding_model}"'),
    ])
    replace_in_file(output / "Services" / "IngestedChunk.cs", [
        (re.compile(r"VectorDimensions = \d+;[^\n]*"),
         f"VectorDimensions = {args.embedding_dimensions}; // vector size of the {args.embedding_model} embedding model"),
        (re.compile(r"MaxTokensPerChunk = \d+;"), f"MaxTokensPerChunk = {args.max_tokens_per_chunk};"),
    ])

    print(f"Created {args.name} in {output}")
    print(f"  chat model:      {args.chat_model}")
    print(f"  embedding model: {args.embedding_model} ({args.embedding_dimensions} dimensions)")
    print(f"  dev URLs:        http://localhost:{http_port}  https://localhost:{https_port}")
    print("Next: ollama pull " + args.embedding_model + "  then  dotnet run --project " + str(output / f"{args.name}.csproj"))


if __name__ == "__main__":
    main()
