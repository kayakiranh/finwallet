#!/usr/bin/env python3
"""Generate TR/EN PDFs from the committed comprehensive glossary source."""

from __future__ import annotations

import importlib.util
from pathlib import Path

from reportlab import rl_config

rl_config.invariant = 1

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
SOURCE = DOCS / "26-technical-financial-glossary.md"
TR_PDF = DOCS / "pdf" / "TR" / "TR_26-technical-financial-glossary.pdf"
EN_PDF = DOCS / "pdf" / "EN" / "EN_26-technical-financial-glossary.pdf"
BASE_GENERATOR = ROOT / "scripts" / "generate_docs_pdf.py"

REQUIRED_MARKERS = (
    "## Türkçe",
    "## English",
    "Bu sözlük 314 terim içerir.",
    "The glossary contains 314 terms.",
    "### Ledger",
    "### Double-Entry Bookkeeping",
    "### Microservices",
)


def load_base_generator():
    spec = importlib.util.spec_from_file_location("finwallet_pdf_base", BASE_GENERATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError("Unable to load the base FinWallet PDF generator.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> None:
    if not SOURCE.is_file():
        raise FileNotFoundError(SOURCE)

    markdown = SOURCE.read_text(encoding="utf-8")
    missing = [marker for marker in REQUIRED_MARKERS if marker not in markdown]
    if missing:
        raise RuntimeError(f"Glossary source is missing required markers: {missing}")

    base = load_base_generator()
    base.register_fonts()
    tr_title, tr_body, en_title, en_body = base.split_document(markdown)

    TR_PDF.parent.mkdir(parents=True, exist_ok=True)
    EN_PDF.parent.mkdir(parents=True, exist_ok=True)
    base.build_pdf(tr_title, tr_body, TR_PDF)
    base.build_pdf(en_title, en_body, EN_PDF)

    print("Generated comprehensive glossary PDFs from committed source.")


if __name__ == "__main__":
    main()
