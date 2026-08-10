#!/usr/bin/env python3
"""Rebuild the comprehensive glossary source and generate its TR/EN PDFs."""

from __future__ import annotations

import base64
import bz2
import hashlib
import importlib.util
from pathlib import Path

from reportlab import rl_config

rl_config.invariant = 1

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
PAYLOAD = ROOT / "scripts" / "glossary_payload"
SOURCE = DOCS / "26-technical-financial-glossary.md"
TR_PDF = DOCS / "pdf" / "TR" / "TR_26-technical-financial-glossary.pdf"
EN_PDF = DOCS / "pdf" / "EN" / "EN_26-technical-financial-glossary.pdf"
BASE_GENERATOR = ROOT / "scripts" / "generate_docs_pdf.py"
EXPECTED_SHA256 = "0c3b319deeee98a57a942705b6562de323598eac6e5294053c90df68917e8966"


def load_base_generator():
    spec = importlib.util.spec_from_file_location("finwallet_pdf_base", BASE_GENERATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError("Unable to load the base FinWallet PDF generator.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def rebuild_source() -> bytes:
    parts = [
        (PAYLOAD / f"part{index:02d}.b64").read_text(encoding="ascii").strip()
        for index in range(1, 6)
    ]

    # The temporary API transport used to seed this large source dropped the
    # first character at three chunk boundaries. Restore those exact boundary
    # characters here. SHA-256 verification below fails closed if any byte is
    # still different from the authoritative generated glossary source.
    encoded = parts[0] + "w" + parts[1] + "/" + parts[2] + "o" + parts[3] + parts[4]
    markdown_bytes = bz2.decompress(base64.b64decode(encoded))
    actual_sha256 = hashlib.sha256(markdown_bytes).hexdigest()
    if actual_sha256 != EXPECTED_SHA256:
        raise RuntimeError(
            f"Glossary source checksum mismatch. Expected {EXPECTED_SHA256}, got {actual_sha256}."
        )

    SOURCE.write_bytes(markdown_bytes)
    return markdown_bytes


def main() -> None:
    markdown_bytes = rebuild_source()
    markdown = markdown_bytes.decode("utf-8")
    if "## Türkçe" not in markdown or "## English" not in markdown:
        raise RuntimeError("Glossary source must contain Turkish and English sections.")

    base = load_base_generator()
    base.register_fonts()
    tr_title, tr_body, en_title, en_body = base.split_document(markdown)

    TR_PDF.parent.mkdir(parents=True, exist_ok=True)
    EN_PDF.parent.mkdir(parents=True, exist_ok=True)
    base.build_pdf(tr_title, tr_body, TR_PDF)
    base.build_pdf(en_title, en_body, EN_PDF)

    print(f"Generated glossary source and PDFs. SHA256={EXPECTED_SHA256}")


if __name__ == "__main__":
    main()
