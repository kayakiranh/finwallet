#!/usr/bin/env python3
"""Generate TR/EN PDFs for the dedicated path and error-code documents."""

from __future__ import annotations

import importlib.util
from pathlib import Path

from reportlab import rl_config

rl_config.invariant = 1

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
TR_OUT = DOCS / "pdf" / "TR"
EN_OUT = DOCS / "pdf" / "EN"
BASE_GENERATOR = ROOT / "scripts" / "generate_docs_pdf.py"
SOURCES = (
    "21-happy-path.md",
    "22-fraud-path.md",
    "23-fail-path.md",
    "24-error-codes.md",
)


def load_base_generator():
    spec = importlib.util.spec_from_file_location("finwallet_pdf_base", BASE_GENERATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError("Unable to load the base FinWallet PDF generator.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> None:
    base = load_base_generator()
    base.register_fonts()
    TR_OUT.mkdir(parents=True, exist_ok=True)
    EN_OUT.mkdir(parents=True, exist_ok=True)

    generated = 0
    for filename in SOURCES:
        source = DOCS / filename
        if not source.is_file():
            raise FileNotFoundError(source)
        tr_title, tr_body, en_title, en_body = base.split_document(source.read_text(encoding="utf-8"))
        base.build_pdf(tr_title, tr_body, TR_OUT / f"TR_{source.stem}.pdf")
        base.build_pdf(en_title, en_body, EN_OUT / f"EN_{source.stem}.pdf")
        generated += 2

    print(f"Generated {generated} path/error PDFs.")


if __name__ == "__main__":
    main()
