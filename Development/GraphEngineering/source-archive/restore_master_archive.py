#!/usr/bin/env python3
"""Restore the uploaded Graph Engineering Master ZIP from Git-safe Base64 chunks."""
from __future__ import annotations

import argparse
import base64
import hashlib
from pathlib import Path

ARCHIVE_BASENAME = "MyUnityMCP_GraphEngineering_Masters.zip"
CHUNK_PATTERN = f"{ARCHIVE_BASENAME}.b64.*"


def restore(source_dir: Path, output: Path) -> tuple[int, str]:
    chunks = sorted(source_dir.glob(CHUNK_PATTERN))
    if not chunks:
        raise RuntimeError(f"archive_chunks_not_found: {source_dir / CHUNK_PATTERN}")

    encoded = "".join(chunk.read_text(encoding="ascii").strip() for chunk in chunks)
    try:
        payload = base64.b64decode(encoded, validate=True)
    except Exception as exc:
        raise RuntimeError("invalid_base64_archive_chunks") from exc

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(payload)
    return len(chunks), hashlib.sha256(payload).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--source-dir",
        type=Path,
        default=Path(__file__).resolve().parent,
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parent / ARCHIVE_BASENAME,
    )
    args = parser.parse_args()

    chunk_count, digest = restore(args.source_dir.resolve(), args.output.resolve())
    print(f"RESTORED: {args.output.resolve()}")
    print(f"CHUNKS: {chunk_count}")
    print(f"SHA256: {digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
